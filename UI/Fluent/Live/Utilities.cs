using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZidWindowsApps.Fluent;
internal sealed partial class LiveWindow
{
    private bool rawReport;
    private readonly List<TroubleItem> checks = TroubleshootingService.GetItems();
    private readonly Dictionary<string, string> results = new();
    private int checkIndex;
    private sealed record PowerPlan(Guid Id, string Name, bool Active);
    private List<PowerPlan> plans = new();
    private PowerPlan? selectedPlan;
    private string folder = KnownFolders.Downloads;
    private List<DuplicateGroup>? duplicates;
    private string? powerError;
    private void Copy(string text)
    {
        try
        {
            Clipboard.SetText(text);
            Notify("Teks disalin.");
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            Notify("Clipboard: " + ex.Message);
        }
    }

    private TextBox ConsoleText(string text) => new()
    {
        Text = text,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        AcceptsReturn = true,
        FontFamily = new FontFamily("Consolas"),
        Background = Paint("#0f131a"),
        Foreground = Paint("#bfdbfe"),
        Padding = new Thickness(14),
        VerticalContentAlignment = VerticalAlignment.Top,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    private UIElement SystemInfo()
    {
        var s = system!;
        var fields = new (string Label, string Value)[]
        {
            ("Sistem operasi", s.OperatingSystem),
            ("Komputer", s.ComputerName),
            ("Prosesor", s.Processor),
            ("Memori", s.Memory),
            ("Grafis", s.Graphics),
            ("Penyimpanan", s.Storage),
            ("Baterai", s.Battery),
            ("Jaringan", s.Network),
            ("Firmware", s.Firmware),
            ("Driver", s.Drivers)
        };
        var report = SystemInfoService.RenderReport(s);
        void Export()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Laporan teks (*.txt)|*.txt",
                FileName = $"ZidWindowsApps-sistem-{DateTime.Now:yyyyMMdd}.txt"
            };
            if (dlg.ShowDialog(this) == true)
                _ = Work("Menyimpan laporan", async (_, _) =>
                {
                    await SystemInfoService.ExportReportAsync(s, dlg.FileName);
                    return "Tersimpan di " + dlg.FileName;
                });
        }

        var top = Card(Row(Segments(new[] { "Visual Cards", "Raw TXT Report" }, rawReport ? "Raw TXT Report" : "Visual Cards", v =>
        {
            rawReport = v == "Raw TXT Report";
            Render();
        }), ActionButton("Export TXT", Export)));
        if (rawReport)
            return Framed(top, Card(ConsoleText(report)), Row(ActionButton("Salin Teks", () => Copy(report))));
        return Framed(top, Scroll(Stack(Columns(3, fields.Select(f => (UIElement)Card(Stack(Label(f.Label, 12, true, "Accent"), Label(f.Value, 14, true)))).ToArray()), Card(Stack(Label("Postur Keamanan Perangkat", 16, true), Label(s.Security))))));
    }

    private UIElement Diagnostics()
    {
        var tool = checks[checkIndex];
        var list = new ListBox
        {
            ItemsSource = checks,
            SelectedIndex = checkIndex,
            DisplayMemberPath = "Name"
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
            {
                checkIndex = list.SelectedIndex;
                Render();
            }
        };
        string output = results.GetValueOrDefault(tool.Name, "Belum dijalankan.");
        void Run()
        {
            if (tool.RequiresAdmin && !AdminMode)
            {
                RequestAdmin();
                return;
            }

            async Task<string> Execute(System.Threading.CancellationToken ct, IProgress<string> progress)
            {
                try
                {
                    results[tool.Name] = await tool.Run(ct, progress);
                    return "Keluaran tersedia di konsol.";
                }
                catch (OperationCanceledException)
                {
                    results[tool.Name] = "Pemeriksaan dibatalkan.";
                    throw;
                }
            }

            if (tool.ChangesSystem || App.Settings.ConfirmBeforeAction)
                ConfirmWork("Jalankan " + tool.Name + "?", tool.Description, "Jalankan", Execute, true);
            else
                _ = Work(tool.Name, Execute, true);
        }

        var save = ActionButton("Simpan log", () =>
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Log teks (*.txt)|*.txt",
                FileName = "Diagnostik.txt"
            };
            if (dlg.ShowDialog(this) == true)
                _ = Work("Menyimpan log", async (_, _) =>
                {
                    await File.WriteAllTextAsync(dlg.FileName, output);
                    return "Log disimpan.";
                });
        });
        var copy = ActionButton("Salin", () => Copy(output));
        save.IsEnabled = copy.IsEnabled = results.ContainsKey(tool.Name);
        return Split(Card(Framed(Label("Daftar Pemeriksaan & Perbaikan (" + checks.Count + ")", 15, true), list)), Card(Framed(Stack(Label(tool.Name, 19, true), Label(tool.Description, 13, false, "Muted"), Label((tool.ChangesSystem ? "Mengubah sistem" : "Pemeriksaan") + (tool.RequiresAdmin ? " · Butuh Administrator" : ""), 12, true, "Accent"), Label("ZidWindowsApps Diagnostic Console", 14, true)), ConsoleText(output), Row(ActionButton("Jalankan pemeriksaan", Run, "primary"), copy, save))), 4, 7);
    }

    private static async Task<string> Power(string args)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(start) ?? throw new IOException("powercfg gagal dimulai.");
        var stdout = proc.StandardOutput.ReadToEndAsync();
        var stderr = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var output = await stdout;
        var err = await stderr;
        if (proc.ExitCode != 0)
            throw new IOException(string.IsNullOrWhiteSpace(err) ? output : err);
        return output;
    }

    private async Task ReadPowerPlans()
    {
        try
        {
            var output = await Power("/list");
            const string pattern = @"(?i)([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})\s+\((.+)\)\s*(\*)?$";
            plans = output.Split('\n').Select(line => Regex.Match(line.Trim(), pattern)).Where(m => m.Success)
                .Select(m => new PowerPlan(Guid.Parse(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Success)).ToList();
            selectedPlan = plans.FirstOrDefault(p => p.Active);
            powerError = plans.Count == 0 ? "Skema daya tidak terbaca." : null;
        }
        catch (Exception ex)
        {
            plans.Clear();
            selectedPlan = null;
            powerError = ex.Message;
        }
    }

    private string? PickFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Pilih folder",
            ShowNewFolderButton = false
        };
        return dlg.ShowDialog(new NativeOwner(new System.Windows.Interop.WindowInteropHelper(this).Handle)) == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
    }

    private sealed record NativeOwner(IntPtr Handle) : System.Windows.Forms.IWin32Window;
    private UIElement ToolsPage()
    {
        var planBar = new WrapPanel();
        foreach (var plan in plans)
        {
            var b = ActionButton(plan.Name, () =>
            {
                selectedPlan = plan;
                Render();
            });
            if (selectedPlan == plan)
                b.SetResourceReference(BackgroundProperty, "Selected");
            planBar.Children.Add(b);
        }

        var apply = ActionButton("Terapkan", () =>
        {
            if (selectedPlan is not PowerPlan plan)
                return;
            ConfirmWork("Ubah rencana daya?", plan.Name, "Terapkan", async (_, _) =>
            {
                await Power("/setactive " + plan.Id.ToString("D"));
                await ReadPowerPlans();
                return plans.Any(p => p.Id == plan.Id && p.Active) ? "Rencana daya diterapkan." : "Status rencana daya belum dapat diverifikasi.";
            });
        }, "primary");
        apply.IsEnabled = selectedPlan is { Active: false };
        var power = Card(Stack(Label("Rencana Daya (Windows Power Plan)", 16, true), Label(powerError ?? ("Rencana aktif · " + (plans.FirstOrDefault(p => p.Active)?.Name ?? "Tidak terbaca")), 13, false, "Muted"), Row(planBar, apply)));
        var path = SearchBox("Folder duplikat", folder);
        path.MinWidth = 280;
        path.TextChanged += (_, _) =>
        {
            folder = path.Text;
            duplicates = null;
        };
        var dupeResults = new StackPanel();
        path.TextChanged += (_, _) => dupeResults.Children.Clear();
        var browse = ActionButton("Browse", () =>
        {
            if (PickFolder() is string value)
                path.Text = value;
        });
        var scan = ActionButton("Pindai duplikat", () => _ = Work("Mencari file duplikat", async (ct, progress) =>
        {
            var dir = PathGuard.Normalize(folder);
            duplicates = null;
            duplicates = await Task.Run(() => DuplicateService.Find(dir, progress, ct));
            return duplicates.Count + " kelompok duplikat ditemukan.";
        }, true), "primary");
        if (duplicates != null)
        {
            dupeResults.Children.Add(Label(duplicates.Count + " kelompok · " + Format.Bytes(duplicates.Sum(g => g.Size * (g.Files.Count - 1))) + " salinan", 13, true));
            var selected = new List<(DuplicateGroup Group, string Path)>();
            var remove = ActionButton("Kirim salinan terpilih ke Recycle Bin", () => ConfirmWork("Pindahkan salinan terpilih?", selected.Count + " file. Salinan pertama setiap kelompok dipertahankan dan isi file diperiksa ulang.", "Pindahkan", async (_, _) =>
            {
                try
                {
                    return await Task.Run(() => RecycleCopies(selected));
                }
                finally
                {
                    duplicates = null;
                }
            }), "danger");
            remove.IsEnabled = false;
            foreach (var g in duplicates.Take(100))
            {
                dupeResults.Children.Add(Label(g.Files[0] + " · Asli · " + Format.Bytes(g.Size), 12, true));
                foreach (var duplicate in g.Files.Skip(1))
                {
                    dupeResults.Children.Add(Check(duplicate, false, v =>
                    {
                        if (v)
                            selected.Add((g, duplicate));
                        else
                            selected.Remove((g, duplicate));
                        remove.IsEnabled = selected.Count > 0;
                    }));
                }
            }

            if (duplicates.Count > 100)
                dupeResults.Children.Add(Label("Menampilkan 100 kelompok terbesar."));
            dupeResults.Children.Add(remove);
        }

        var dupe = Card(Stack(Label("Pencari File Duplikat (SHA-256 Hash)", 16, true), Row(path, browse, scan), dupeResults));
        void Open(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Notify(ex.Message);
            }
        }

        var battery = ActionButton("Buat laporan baterai", () => _ = Work("Membuat laporan baterai", async (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var dest = Path.Combine(AppPaths.DataDirectory, "battery-report.html");
            await Power("/batteryreport /output \"" + dest + "\"");
            if (!File.Exists(dest))
                throw new IOException("Laporan tidak ditemukan.");
            Open(dest);
            return "Laporan disimpan di " + dest;
        }));
        return Scroll(Stack(power, dupe, Card(Stack(Label("Utilitas & Pintasan Windows", 16, true), Row(battery, ActionButton("Pengaturan Windows", () => Open("ms-settings:")), ActionButton("Disk Management", () => Open(Path.Combine(Environment.SystemDirectory, "diskmgmt.msc"))), ActionButton("Startup Apps", () => Open("ms-settings:startupapps")))))));
    }

    private string RecycleCopies(List<(DuplicateGroup Group, string Path)> selected)
    {
        int ok = 0;
        var errors = new List<string>();
        var rootPath = PathGuard.Normalize(folder);
        foreach (var (group, path) in selected)
        {
            try
            {
                if (!PathGuard.IsUnder(path, rootPath) || PathGuard.IsProtected(path) || PathGuard.IsExcluded(path, App.Settings.ExcludedPaths) || string.Equals(path, group.Files[0], StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Lokasi dilindungi.");
                for (var d = new FileInfo(path).Directory; d != null; d = d.Parent)
                    if (d.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        throw new IOException("Lokasi berubah.");
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException("Lokasi berubah.");
                using var original = File.Open(group.Files[0], FileMode.Open, FileAccess.Read, FileShare.Read);
                using var copy = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                if (Convert.ToHexString(SHA256.HashData(original)) != group.Hash || Convert.ToHexString(SHA256.HashData(copy)) != group.Hash)
                    throw new IOException("Isi file berubah sejak pemindaian.");
                if (!RecycleBinService.TrySend(path, out string? error))
                    throw new IOException(error);
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        return $"{ok} dipindahkan, {errors.Count} gagal. " + errors.FirstOrDefault();
    }
}
