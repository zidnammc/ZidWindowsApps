using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ZidWindowsApps.Fluent;
internal sealed partial class LiveWindow
{
    private bool cleanedFiles;
    private bool showSystem, includeBrowser = true, includeDownloads;
    private string processQuery = "", processOrder = "RAM Tertinggi", startupQuery = "", startupFilter = "Semua", appQuery = "", appFilter = "Semua";
    private InstalledApp? inspected, removedApp;
    private bool showRemoved;
    private List<LeftoverItem> leftovers = new();
    private UIElement Processes()
    {
        var table = Table("Daftar proses", ("Nama proses", "Name", 175), ("PID", "Id", 65), ("CPU %", "Cpu", 74), ("Memori (RAM)", "Memory", 105), ("Status", "Classification", 110), ("Publisher", "Publisher", 165), ("Lokasi", "Path", 360));
        table.Columns[3].SortMemberPath = "MemoryBytes";
        var query = SearchBox("Nama, PID, publisher", processQuery);
        var count = Label("", 12, false, "Muted");
        bool Allowed(ProcessInfo p) => p.Id != Environment.ProcessId && !p.IsSystem && !ProcessService.IsCritical(p) && p.StartTime.HasValue;
        void Filter()
        {
            var selected = table.SelectedItem as ProcessInfo;
            var list = processes.Where(p => (showSystem || !p.IsSystem) && Matches(p.Name + " " + p.Id + " " + p.Publisher + " " + p.Path, processQuery));
            var rows = (processOrder switch
            {
                "CPU Tertinggi" => list.OrderByDescending(p => p.Cpu),
                "Nama (A-Z)" => list.OrderBy(p => p.Name),
                _ => list.OrderByDescending(p => p.MemoryBytes)}).ToList();
            table.ItemsSource = rows;
            table.SelectedItem = rows.FirstOrDefault(p => p.Id == selected?.Id && p.StartTime == selected.StartTime);
            count.Text = rows.Count + " proses ditampilkan";
        }

        void Stop(bool tree)
        {
            if (table.SelectedItem is not ProcessInfo proc || !Allowed(proc))
                return;
            ConfirmWork(tree ? "Hentikan Process Tree?" : "Hentikan Proses?", proc.Name + " · PID " + proc.Id + (tree ? " beserta seluruh subproses." : "."), "Hentikan", async (_, _) =>
            {
                await Task.Run(() => ProcessService.Kill(proc, tree));
                ready.Remove("processes");
                return "Proses telah berhenti.";
            });
        }

        var stop = ActionButton("Hentikan Proses", () => Stop(false), "danger");
        var tree = ActionButton("Hentikan Process Tree", () => Stop(true), "danger");
        stop.IsEnabled = tree.IsEnabled = false;
        table.SelectionChanged += (_, _) => stop.IsEnabled = tree.IsEnabled = table.SelectedItem is ProcessInfo p && Allowed(p);
        table.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && stop.IsEnabled)
            {
                Stop(false);
                e.Handled = true;
            }
        };
        updateProcesses = Filter;
        Filter();
        query.TextChanged += (_, _) =>
        {
            processQuery = query.Text;
            Filter();
        };
        return Framed(Card(Stack(Row(SearchField("Cari proses", query), Check("Tampilkan proses sistem", showSystem, v =>
        {
            showSystem = v;
            Filter();
        }), Segments(new[] { "RAM Tertinggi", "CPU Tertinggi", "Nama (A-Z)" }, processOrder, v =>
        {
            processOrder = v;
            Filter();
        })), Row(stop, tree))), Card(table), count);
    }

    private UIElement Startup()
    {
        var table = Table("Daftar auto-start", ("Nama item", "Name", 210), ("Lokasi startup", "Location", 145), ("Perintah / Executable", "Command", 500));
        table.SelectionMode = DataGridSelectionMode.Extended;
        var query = SearchBox("Nama startup atau lokasi", startupQuery);
        void Filter() => table.ItemsSource = startup.Where(s => (startupFilter == "Semua" || s.StatusText == startupFilter) && Matches(s.Name + " " + s.Location, startupQuery)).ToList();
        void Change(IEnumerable<StartupItem> selection, bool enable)
        {
            var targets = selection.Where(s => s.IsEnabled != enable).ToList();
            if (targets.Count == 0)
            {
                Notify("Pilih item dengan status yang sesuai.");
                return;
            }

            if (!AdminMode && targets.Any(i => i.IsMachineWide || i.Kind == StartupKind.WindowsService))
            {
                RequestAdmin();
                return;
            }

            string verb = enable ? "Aktifkan" : "Nonaktifkan";
            ConfirmWork(verb + " auto-start?", string.Join(", ", targets.Take(4).Select(x => x.Name)) + " · " + targets.Count + " item. Perubahan layanan memengaruhi startup Windows.", verb, async (_, _) =>
            {
                var failures = new List<string>();
                await Task.Run(() =>
                {
                    foreach (var s in targets)
                        try
                        {
                            StartupService.SetEnabled(s, enable);
                        }
                        catch (Exception ex)
                        {
                            failures.Add(s.Name + ": " + ex.Message);
                        }
                });
                ready.Remove("autostart");
                return (targets.Count - failures.Count) + " item diperbarui." + (failures.Count > 0 ? " Gagal: " + string.Join("; ", failures.Take(3)) : "");
            });
        }

        var factory = new FrameworkElementFactory(typeof(CheckBox));
        factory.SetValue(StyleProperty, FindResource("Switch"));
        factory.SetBinding(CheckBox.IsCheckedProperty, new Binding("IsEnabled") { Mode = BindingMode.OneWay });
        factory.SetBinding(ContentControl.ContentProperty, new Binding("StatusText"));
        factory.SetBinding(AutomationProperties.NameProperty, new Binding("Name"));
        factory.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler((s, _) =>
        {
            var b = (CheckBox)s;
            if (b.DataContext is StartupItem i)
            {
                b.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateTarget();
                Change(new[] { i }, !i.IsEnabled);
            }
        }));
        table.Columns.Insert(0, new DataGridTemplateColumn { Header = "Status", Width = 115, CellTemplate = new DataTemplate { VisualTree = factory } });
        Filter();
        query.TextChanged += (_, _) =>
        {
            startupQuery = query.Text;
            Filter();
        };
        return Framed(Card(Stack(Row(Segments(new[] { "Semua", "Aktif", "Nonaktif" }, startupFilter, v =>
        {
            startupFilter = v;
            Filter();
        }), SearchField("Cari", query)), Label("Menonaktifkan item mempertahankan entri agar dapat diaktifkan kembali.", 12, false, "Muted"))), Card(table), Row(ActionButton("Aktifkan pilihan", () => Change(table.SelectedItems.Cast<StartupItem>(), true)), ActionButton("Nonaktifkan pilihan", () => Change(table.SelectedItems.Cast<StartupItem>(), false))));
    }

    private UIElement AppsPage()
    {
        var table = Table("Aplikasi terpasang", ("Aplikasi", "Name", 190), ("Tipe", "TypeText", 75), ("Versi", "Version", 115), ("Publisher", "Publisher", 180));
        var query = SearchBox("Nama aplikasi atau publisher", appQuery);
        var pane = new ContentControl();
        void Detail()
        {
            var app = showRemoved ? removedApp : inspected;
            if (app == null)
            {
                pane.Content = Card(Label("Pilih aplikasi untuk melihat detail."));
                return;
            }

            var details = Stack(Label(app.TypeText, 12, true, "Accent"), Label(app.Name, 20, true), Label(app.Publisher + " · " + app.Version), Label(app.InstallLocation, 12, false, "Muted"));
            if (app.SizeBytes.HasValue || app.InstallDate.Length > 0)
                details.Children.Add(Label(string.Join(" · ", new[] { app.SizeBytes.HasValue ? Format.Bytes(app.SizeBytes.Value) : "", app.InstallDate }.Where(x => x.Length > 0)), 12, false, "Muted"));
            if (!showRemoved)
            {
                var uninstall = ActionButton("Uninstall Aplikasi", () => ConfirmWork("Uninstall " + app.Name + "?", app.IsStoreApp ? (AdminMode ? "Paket Store ini akan dihapus untuk semua pengguna." : "Paket Store ini akan dihapus untuk pengguna saat ini.") : "Proses dari folder instalasi akan ditutup dan uninstaller resmi akan dijalankan.", "Mulai Uninstall", async (_, _) =>
                {
                    var outcome = await Task.Run(() => InstalledAppsService.UninstallFully(app, 10));
                    ready.Remove("uninstaller");
                    if (outcome.Removed)
                    {
                        removedApp = app;
                        showRemoved = true;
                        leftovers.Clear();
                        inspected = null;
                    }

                    return outcome.Message;
                }), "danger");
                uninstall.IsEnabled = !app.Name.Contains("ZidWindowsApps", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(app.InstallLocation) || !PathGuard.IsUnder(AppContext.BaseDirectory, app.InstallLocation));
                details.Children.Add(uninstall);
                details.Children.Add(Label("Pencarian leftover tersedia setelah uninstall selesai.", 12, false, "Muted"));
            }
            else
            {
                details.Children.Add(Label("Sisa file & registry", 16, true));
                details.Children.Add(ActionButton("Pindai leftover", () => _ = Work("Mencari leftover", async (_, _) =>
                {
                    var excluded = App.Settings.ExcludedPaths.ToList();
                    leftovers = await Task.Run(() => InstalledAppsService.FindLeftovers(app, excluded));
                    return leftovers.Count + " kandidat ditemukan.";
                })));
                var selected = new List<LeftoverItem>();
                var remove = ActionButton("Hapus leftover terpilih", () => ConfirmWork("Hapus leftover terpilih?", selected.Count + " item milik " + app.Name + ". File masuk Recycle Bin dan key registry dicadangkan ke " + AppPaths.RegistryBackupDirectory + ".", "Hapus", async (_, _) =>
                {
                    var succeeded = new List<LeftoverItem>();
                    var failures = new List<string>();
                    await Task.Run(() =>
                    {
                        foreach (var item in selected)
                            if (InstalledAppsService.TryRemoveLeftover(item, out var err))
                                succeeded.Add(item);
                            else
                                failures.Add(err);
                    });
                    leftovers.RemoveAll(succeeded.Contains);
                    return succeeded.Count + " dihapus, " + failures.Count + " gagal. " + failures.FirstOrDefault();
                }), "danger");
                remove.IsEnabled = false;
                foreach (var item in leftovers)
                {
                    var b = Check("", false, v =>
                    {
                        if (v)
                            selected.Add(item);
                        else
                            selected.Remove(item);
                        remove.IsEnabled = selected.Count > 0;
                    });
                    b.Content = Label(item.Display, 12);
                    details.Children.Add(b);
                }

                details.Children.Add(remove);
            }

            pane.Content = Card(Scroll(details));
        }

        void Filter()
        {
            var rows = apps.Where(a => (appFilter == "Semua" || a.TypeText == appFilter) && Matches(a.Name + " " + a.Publisher, appQuery)).ToList();
            var next = rows.Contains(inspected!) ? inspected : rows.FirstOrDefault();
            bool wasRemoved = showRemoved;
            table.ItemsSource = rows;
            table.SelectedItem = wasRemoved ? null : next;
            inspected = next;
            showRemoved = wasRemoved;
            Detail();
        }

        table.SelectionChanged += (_, _) =>
        {
            if (table.SelectedItem is InstalledApp app)
            {
                inspected = app;
                showRemoved = false;
                Detail();
            }
        };
        Filter();
        query.TextChanged += (_, _) =>
        {
            appQuery = query.Text;
            Filter();
        };
        var bar = Row(SearchField("Cari aplikasi", query), Segments(new[] { "Semua", "Desktop", "Store" }, appFilter, v =>
        {
            appFilter = v;
            Filter();
        }));
        if (removedApp != null)
            bar.Children.Add(ActionButton("Leftover uninstall terakhir", () =>
            {
                showRemoved = true;
                table.SelectedItem = null;
                Detail();
            }));
        return Framed(Card(bar), Split(Card(table), pane));
    }

    private void ScanCache() => _ = Work("Memindai kategori pembersihan", async (ct, _) =>
    {
        cache = await Task.Run(() => new CleanupService(App.Settings).Scan(includeBrowser, includeDownloads, ct));
        scanned = true;
        return cache.Count + " kategori ditemukan.";
    }, true);
    private UIElement Cleanup()
    {
        var table = Table("Kategori pembersihan", ("Kategori", "Category", 215), ("File", "FileCount", 75), ("Ukuran", "SizeText", 95), ("Lokasi", "Path", 450));
        var total = Label("", 14, true);
        var clean = ActionButton("Kirim Terpilih ke Recycle Bin", () =>
        {
            var rows = cache.Where(x => x.Selected).ToList();
            if (rows.Count == 0)
                return;
            ConfirmWork("Kirim file terpilih ke Recycle Bin?", rows.Sum(x => x.FileCount) + " file · " + Format.Bytes(rows.Sum(x => x.Size)) + ". " + (rows.Any(r => r.Category.StartsWith("Downloads")) ? "Termasuk Downloads. " : "") + "Tutup browser sebelum membersihkan cache.", "Kirim Terpilih", async (ct, progress) =>
            {
                try
                {
                    var result = await Task.Run(() => new CleanupService(App.Settings).Delete(rows, false, progress, ct));
                    cleanedFiles |= result.Deleted > 0;
                    return $"{result.Deleted} file dipindahkan ({Format.Bytes(result.FreedBytes)}), {result.Skipped} dilewati, {result.Failed} gagal.";
                }
                finally
                {
                    cache.Clear();
                    scanned = false;
                }
            });
        }, "primary");
        var selectAll = new CheckBox
        {
            Content = "Semua",
            ToolTip = "Pilih semua kategori yang terlihat"
        };
        void Total()
        {
            var rows = cache.Where(x => x.Selected).ToList();
            total.Text = "Terpilih · " + Format.Bytes(rows.Sum(x => x.Size));
            clean.IsEnabled = rows.Count > 0;
            selectAll.IsChecked = rows.Count == 0 ? false : rows.Count == cache.Count ? true : null;
        }

        selectAll.Click += (_, _) =>
        {
            foreach (var item in cache)
                item.Selected = selectAll.IsChecked == true;
            Total();
        };
        var box = new FrameworkElementFactory(typeof(CheckBox));
        box.SetBinding(CheckBox.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        box.SetBinding(AutomationProperties.NameProperty, new Binding("Category"));
        box.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler((_, _) => Total()));
        table.Columns.Insert(0, new DataGridTemplateColumn { Header = selectAll, Width = 86, CellTemplate = new DataTemplate { VisualTree = box } });
        table.ItemsSource = cache;
        Total();
        void Reset()
        {
            scanned = false;
            cache = new();
            Render();
        }

        var header = Card(Stack(Label(scanned ? Format.Bytes(cache.Sum(x => x.Size)) + " siap diperiksa" : "Belum dipindai", 21, true), Label("Centang kategori yang ingin dipindahkan ke Recycle Bin.", 13, false, "Muted"), Row(Check("Cache browser", includeBrowser, v =>
        {
            includeBrowser = v;
            Reset();
        }), Check("Analisis Downloads (>30 hari)", includeDownloads, v =>
        {
            includeDownloads = v;
            Reset();
        }), ActionButton("Scan Aman", ScanCache))));
        var footer = Stack(Row(total, clean, ActionButton("Cari file duplikat", () => Navigate("tools"))));
        if (cleanedFiles)
            footer.Children.Add(Row(Label("Ruang disk bertambah setelah Recycle Bin dikosongkan.", 12, false, "Muted"), ActionButton("Kosongkan Recycle Bin (" + drive + ")", () =>
            {
                try
                {
                    var info = RecycleBinService.ReadSize(drive);
                    if (info.Items == 0)
                    {
                        Notify("Recycle Bin kosong.");
                        return;
                    }

                    ConfirmWork("Kosongkan Recycle Bin " + drive + "?", info.Items + " item (" + Format.Bytes(info.Bytes) + ") pada drive ini akan dihapus permanen.", "Kosongkan", async (_, _) =>
                    {
                        await Task.Run(() => RecycleBinService.Empty(drive));
                        cleanedFiles = false;
                        return "Recycle Bin " + drive + " dikosongkan.";
                    });
                }
                catch (Exception ex)
                {
                    Notify("Ukuran Recycle Bin tidak dapat dibaca: " + ex.Message);
                }
            }, "danger")));
        return Framed(header, scanned ? Card(table) : Card(Label("Pilih Scan Aman untuk membaca ukuran file.", 14, false, "Muted")), Card(footer));
    }
}
