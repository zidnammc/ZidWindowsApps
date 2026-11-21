using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZidWindowsApps.Fluent;
internal sealed partial class PreviewWindow
{
    private bool rawReport;
    private int diagnosticIndex;
    private readonly Dictionary<string, string> outputs = new();
    private CancellationTokenSource? diagnosticRun;
    private string plan = "Seimbang", pendingPlan = "Seimbang";
    private string duplicateFolder = @"C:\Contoh\Downloads";
    private bool scannedDuplicates, duplicateRemoved;
    private UIElement SystemInfo()
    {
        var fields = new (string Name, string Value)[]
        {
            ("Sistem operasi", "Windows 11 Home Single Language 25H2"),
            ("Prosesor", "AMD 3020e with Radeon Graphics · 2 thread"),
            ("Memori", "3,38 GB total · 384,1 MB tersedia"),
            ("Grafis", "AMD Radeon Graphics · 31.0.21024.2004"),
            ("Penyimpanan", "C: · 237,23 GB total · 99,67 GB bebas"),
            ("Baterai", "34% · Menggunakan baterai"),
            ("Jaringan", "Wi-Fi · Qualcomm Atheros QCA61x4A · 144,4 Mbps"),
            ("Firmware", "LENOVO E8CN31WW"),
            ("Driver", "1 driver tidak bertanda tangan")
        };
        string security = admin ? "TPM 2.0 aktif · Secure Boot aktif · BitLocker terlindungi" : "TPM, Secure Boot dan BitLocker membutuhkan inspeksi Administrator.";
        var report = "LAPORAN CONTOH · ZidWindowsApps\n\n" + string.Join("\n\n", fields.Select(f => "[" + f.Name + "]\n" + f.Value)) + "\n\n[Keamanan]\n" + security;
        void Export()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Laporan teks (*.txt)|*.txt",
                FileName = "ZidWindowsApps-contoh.txt"
            };
            if (dialog.ShowDialog(this) != true)
                return;
            try
            {
                File.WriteAllText(dialog.FileName, report);
                Notify("Laporan contoh disimpan.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Notify("Gagal menyimpan: " + ex.Message);
            }
        }

        var toolbar = Card(Row(Segments(new[] { "Visual Cards", "Raw TXT Report" }, rawReport ? "Raw TXT Report" : "Visual Cards", v =>
        {
            rawReport = v == "Raw TXT Report";
            Navigate(current);
        }), ActionButton("Export TXT", Export)));
        if (rawReport)
        {
            var copy = ActionButton("Salin Teks", () =>
            {
                try
                {
                    Clipboard.SetText(report);
                    Notify("Laporan contoh disalin.");
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    Notify("Clipboard sedang digunakan.");
                }
            });
            return Framed(toolbar, Card(ConsoleBox(report)), Row(copy));
        }

        var tiles = fields.Select(f => (UIElement)Card(Stack(Label(f.Name, 12, true, "Accent"), Label(f.Value, 15, true)))).ToArray();
        return Framed(toolbar, Scroll(Stack(Columns(3, tiles), Card(Stack(Label("Postur Keamanan Perangkat", 16, true), Label(security))))));
    }

    private static TextBox ConsoleBox(string text) => new()
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
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        MinHeight = 90
    };
    private UIElement Diagnostics()
    {
        var items = data.Tools;
        var list = new ListBox
        {
            ItemsSource = items,
            DisplayMemberPath = "Title",
            SelectedIndex = diagnosticIndex
        };
        var pane = new ContentControl();
        void Detail()
        {
            var tool = items[diagnosticIndex];
            var log = ConsoleBox(outputs.GetValueOrDefault(tool.Title, "Pilih Jalankan untuk melihat keluaran contoh."));
            Button? run = null;
            async Task Execute()
            {
                if (diagnosticRun != null)
                    return;
                using var source = new CancellationTokenSource();
                diagnosticRun = source;
                list.IsEnabled = false;
                run!.Content = "Batalkan";
                log.Clear();
                var lines = tool.Title.Contains("(SFC)", StringComparison.Ordinal) ? new[]
                {
                    "[CONTOH] Pemeriksaan file sistem: sfc /scannow",
                    "[DONE] Simulasi selesai."
                }

                : tool.Logs.ToArray();
                try
                {
                    foreach (var line in lines)
                    {
                        await Task.Delay(250, source.Token);
                        log.AppendText(line + Environment.NewLine);
                        log.ScrollToEnd();
                    }

                    Notify("Simulasi diagnostik selesai.");
                }
                catch (OperationCanceledException)
                {
                    log.AppendText("[INFO] Simulasi dibatalkan.");
                }
                finally
                {
                    outputs[tool.Title] = log.Text;
                    diagnosticRun = null;
                    list.IsEnabled = true;
                    run.Content = "Jalankan pemeriksaan";
                }
            }

            run = ActionButton("Jalankan pemeriksaan", () =>
            {
                if (diagnosticRun != null)
                {
                    diagnosticRun.Cancel();
                    return;
                }

                if (tool.RequiresAdmin && !admin)
                {
                    RequestSimulatedAdmin();
                    return;
                }

                Ask("Jalankan " + tool.Title + "?", tool.Risk + " · Keluaran diambil dari data contoh.", "Jalankan", () => _ = Execute());
            }, "primary");
            var copy = ActionButton("Salin", () => CopyText(log.Text));
            var save = ActionButton("Simpan log", () =>
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Log teks (*.txt)|*.txt",
                    FileName = "Diagnostik-contoh.txt"
                };
                if (dlg.ShowDialog(this) != true)
                    return;
                try
                {
                    File.WriteAllText(dlg.FileName, log.Text);
                    Notify("Log contoh disimpan.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Notify("Gagal menyimpan: " + ex.Message);
                }
            });
            pane.Content = Card(Framed(Stack(Label(tool.Title, 19, true), Label(tool.Desc, 13, false, "Muted"), Label(tool.Risk + (tool.RequiresAdmin ? "  ·  Butuh Administrator" : ""), 12, true, "Accent"), Label("ZidWindowsApps Diagnostic Console", 14, true)), log, Row(run, copy, save)));
        }

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
            {
                diagnosticIndex = list.SelectedIndex;
                Detail();
            }
        };
        Detail();
        return Split(Card(Framed(Label("Daftar Pemeriksaan & Perbaikan (" + items.Count + ")", 15, true), list)), pane, 4, 7);
    }

    private UIElement Tools()
    {
        var plans = Segments(new[] { "Hemat Daya", "Seimbang", "Performa Tinggi" }, pendingPlan, v => pendingPlan = v);
        var power = Card(Stack(Label("Rencana Daya (Windows Power Plan)", 16, true), Label("Rencana aktif · " + plan, 13, false, "Muted"), Row(plans, ActionButton("Terapkan", () => Ask("Terapkan rencana daya?", pendingPlan + " pada perangkat contoh.", "Terapkan", () =>
        {
            plan = pendingPlan;
            Navigate(current);
            Notify("Rencana daya contoh diterapkan.");
        }), "primary"))));
        var path = SearchBox("Folder duplikat", duplicateFolder);
        path.MinWidth = 260;
        path.IsReadOnly = true;
        var browse = ActionButton("Browse", () =>
        {
            var picker = new OpenFolderDialog
            {
                Title = "Pilih folder untuk simulasi"
            };
            if (picker.ShowDialog(this) == true)
            {
                duplicateFolder = picker.FolderName;
                scannedDuplicates = false;
                duplicateRemoved = false;
                Navigate(current);
            }
        });
        var dupe = Stack(Label("Pencari File Duplikat (SHA-256 Hash)", 16, true), Label("Pemindaian pratinjau menggunakan sepasang file contoh.", 13, false, "Muted"), Row(path, browse, ActionButton("Pindai duplikat", () =>
        {
            scannedDuplicates = true;
            Navigate(current);
        }, "primary")));
        if (scannedDuplicates)
        {
            if (duplicateRemoved)
                dupe.Children.Add(Label("Tidak ada duplikat tersisa di data contoh."));
            else
            {
                var remove = ActionButton("Kirim salinan ke Recycle Bin", () => Ask("Pindahkan salinan contoh?", Path.Combine(duplicateFolder, "Laporan (salinan).pdf"), "Pindahkan", () =>
                {
                    duplicateRemoved = true;
                    Navigate(current);
                    Notify("Salinan contoh dipindahkan.");
                }), "danger");
                remove.IsEnabled = false;
                dupe.Children.Add(Label("Laporan.pdf · Asli", 13, true));
                dupe.Children.Add(Check("Laporan (salinan).pdf · 2,4 MB", false, v => remove.IsEnabled = v));
                dupe.Children.Add(remove);
            }
        }

        return Scroll(Stack(power, Card(dupe), Card(Stack(Label("Utilitas & Pintasan Windows", 16, true), Row(Unavailable("Laporan baterai"), Unavailable("Pengaturan Windows"), Unavailable("Disk Management"), Unavailable("Startup Apps"))))));
    }

    private UIElement Settings()
    {
        var saved = PreviewPreferences.Load();
        bool Dirty() => prefs.Tray != saved.Tray || prefs.HideSystem != saved.HideSystem || prefs.Confirm != saved.Confirm || prefs.Schedule != saved.Schedule || prefs.Frequency != saved.Frequency || !prefs.Exclusions.SequenceEqual(saved.Exclusions);
        var dirty = Label("", 12, false, "Muted");
        Button? save = null;
        void UpdateSave()
        {
            if (save != null)
                save.IsEnabled = Dirty();
            dirty.Text = Dirty() ? "Perubahan belum disimpan" : "";
        }

        var general = Card(Stack(Label("Umum & Tampilan", 16, true), Label("Bahasa antarmuka · Bahasa Indonesia"), Row(Label("Tema antarmuka"), Segments(new[] { "Terang", "Gelap" }, prefs.Dark ? "Gelap" : "Terang", v =>
        {
            if (prefs.Dark != (v == "Gelap"))
                ToggleTheme();
        }))));
        var behavior = Card(Stack(Label("Perilaku & Keamanan", 16, true), Check("Tetap berjalan di system tray saat window ditutup", prefs.Tray, v =>
        {
            prefs.Tray = v;
            UpdateSave();
        }), Check("Sembunyikan proses sistem secara default", prefs.HideSystem, v =>
        {
            prefs.HideSystem = v;
            showSystem = !v;
            UpdateSave();
        }), Check("Minta konfirmasi sebelum tindakan", prefs.Confirm, v =>
        {
            prefs.Confirm = v;
            UpdateSave();
        }), Label("Tindakan berisiko selalu meminta konfirmasi.", 12, false, "Muted")));
        var frequency = Segments(new[] { "Harian", "Mingguan", "Bulanan" }, prefs.Frequency, v =>
        {
            prefs.Frequency = v;
            UpdateSave();
        });
        frequency.IsEnabled = prefs.Schedule;
        var schedule = Stack(Label("Pembersihan Terjadwal", 16, true), Check("Aktifkan pembersihan terjadwal (simulasi)", prefs.Schedule, v =>
        {
            prefs.Schedule = v;
            frequency.IsEnabled = v;
            UpdateSave();
        }), frequency);
        var exclude = ActionButton("Tambah folder pengecualian", () =>
        {
            var picker = new OpenFolderDialog
            {
                Title = "Pilih folder pengecualian contoh"
            };
            if (picker.ShowDialog(this) != true)
                return;
            if (!prefs.Exclusions.Contains(picker.FolderName, StringComparer.OrdinalIgnoreCase))
                prefs.Exclusions.Add(picker.FolderName);
            Navigate(current);
        });
        schedule.Children.Add(exclude);
        foreach (var folder in prefs.Exclusions.ToArray())
            schedule.Children.Add(Row(Label(folder, 12), ActionButton("Keluarkan", () =>
            {
                prefs.Exclusions.Remove(folder);
                Navigate(current);
            })));
        UIElement Location(string title, string path) => Framed(Label(title, 12, true, "Muted"), Row(Label(path, 12), ActionButton("Salin", () => CopyText(path))));
        var locations = Card(Stack(Label("Log & Lokasi Data", 16, true), Location("Pengaturan pratinjau", PreviewPreferences.FileName), Location("Log aktivitas", AppPaths.LogsDirectory), Location("Cadangan registry", AppPaths.RegistryBackupDirectory), Unavailable("Hapus semua log aktivitas")));
        save = ActionButton("Simpan Pengaturan", () =>
        {
            try
            {
                prefs.Save();
                saved = PreviewPreferences.Load();
                UpdateSave();
                Notify("Pengaturan pratinjau tersimpan.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Notify("Gagal menyimpan: " + ex.Message);
            }
        }, "primary");
        UpdateSave();
        var actions = Row(dirty, save);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        var stack = Stack(general, behavior, Card(schedule), locations, actions);
        stack.MaxWidth = 900;
        return Scroll(stack);
    }

    private void CopyText(string value)
    {
        try
        {
            Clipboard.SetText(value);
            Notify("Teks disalin.");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Notify("Clipboard sedang digunakan.");
        }
    }
}
