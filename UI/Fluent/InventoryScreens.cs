using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ZidWindowsApps.Fluent;
internal sealed partial class PreviewWindow
{
    private string processQuery = "", startupQuery = "", appQuery = "";
    private string processOrder = "RAM Tertinggi";
    private string startupFilter = "Semua", appFilter = "Semua";
    private bool showSystem, browserCache = true, oldDownloads;
    private readonly HashSet<AppRow> scannedApps = new();
    private AppRow? inspected;
    private AppRow? lastUninstalled;
    private UIElement Processes()
    {
        var table = Table("Daftar proses", ("Nama proses", "Name", 175), ("PID", "Pid", 65), ("CPU %", "Cpu", 74), ("Memori (RAM)", "Memory", 105), ("Status", "Status", 100), ("Publisher", "Publisher", 150), ("Lokasi", "Path", 360));
        table.Columns[3].SortMemberPath = "RamMb";
        var query = SearchBox("Nama, PID, publisher", processQuery);
        var count = Label("", 12, false, "Muted");
        void Filter()
        {
            var matches = data.Processes.Where(p => (showSystem || !p.IsSystem) && Matches(p.Name + " " + p.Pid + " " + p.Publisher + " " + p.Path, processQuery));
            var rows = (processOrder switch
            {
                "CPU Tertinggi" => matches.OrderByDescending(p => p.Cpu),
                "Nama (A-Z)" => matches.OrderBy(p => p.Name),
                _ => matches.OrderByDescending(p => p.RamMb)}).ToList();
            table.ItemsSource = rows;
            count.Text = rows.Count == 0 ? "Tidak ada proses yang cocok." : rows.Count + " proses ditampilkan";
        }

        void Stop(bool tree)
        {
            if (table.SelectedItem is not ProcessRow p || p.Protected)
                return;
            var targets = tree ? data.ProcessTree(p) : new List<ProcessRow>
            {
                p
            };
            if (targets.Any(x => x.Protected))
            {
                Notify("Pilihan memuat proses yang dilindungi.");
                return;
            }

            Ask(tree ? "Hentikan Process Tree?" : "Hentikan Proses?", p.Name + " · PID " + p.Pid + " · " + targets.Count + " proses contoh.", "Hentikan", () =>
            {
                data.Processes.RemoveAll(targets.Contains);
                Filter();
                Sidebar();
                Notify(targets.Count + " proses contoh dihentikan.");
            });
        }

        var kill = ActionButton("Hentikan Proses", () => Stop(false), "danger");
        var killTree = ActionButton("Hentikan Process Tree", () => Stop(true), "danger");
        kill.IsEnabled = killTree.IsEnabled = false;
        table.SelectionChanged += (_, _) => kill.IsEnabled = killTree.IsEnabled = table.SelectedItem is ProcessRow { Protected: false };
        table.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Delete && kill.IsEnabled)
            {
                Stop(false);
                e.Handled = true;
            }
        };
        query.TextChanged += (_, _) =>
        {
            processQuery = query.Text;
            Filter();
        };
        var sort = Segments(new[] { "RAM Tertinggi", "CPU Tertinggi", "Nama (A-Z)" }, processOrder, v =>
        {
            processOrder = v;
            Filter();
        });
        var toolbar = Stack(Row(SearchField("Cari proses", query), Check("Tampilkan proses sistem", showSystem, v =>
        {
            showSystem = v;
            Filter();
        }), sort), Row(kill, killTree));
        Filter();
        return Framed(Card(toolbar), Card(table), count);
    }

    private UIElement Startup()
    {
        var table = Table("Daftar auto-start", ("Nama item", "Name", 210), ("Lokasi startup", "Location", 135), ("Perintah / Executable", "Command", 560));
        var query = SearchBox("Nama startup atau lokasi", startupQuery);
        void Filter() => table.ItemsSource = data.Startup.Where(s => (startupFilter == "Semua" || s.Status == startupFilter) && Matches(s.Name + " " + s.Location, startupQuery)).ToList();
        void Change(StartupRow s)
        {
            if (!admin && !s.Location.StartsWith("HKCU"))
            {
                RequestSimulatedAdmin();
                return;
            }

            Ask((s.Enabled ? "Nonaktifkan " : "Aktifkan ") + s.Name + "?", s.Location + " · " + s.Command, "Ubah status", () =>
            {
                data.Startup[data.Startup.IndexOf(s)] = s with
                {
                    Enabled = !s.Enabled
                };
                Filter();
                Sidebar();
                Notify("Status auto-start contoh diperbarui.");
            });
        }

        var toggle = new FrameworkElementFactory(typeof(CheckBox));
        toggle.SetValue(StyleProperty, FindResource("Switch"));
        toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding("Enabled") { Mode = BindingMode.OneWay });
        toggle.SetBinding(ContentControl.ContentProperty, new Binding("Status"));
        toggle.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty, new Binding("Name"));
        toggle.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler((sender, _) =>
        {
            var box = (CheckBox)sender;
            if (box.DataContext is StartupRow s)
            {
                box.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateTarget();
                Change(s);
            }
        }));
        table.Columns.Insert(0, new DataGridTemplateColumn { Header = "Status", Width = 110, CellTemplate = new DataTemplate { VisualTree = toggle } });
        query.TextChanged += (_, _) =>
        {
            startupQuery = query.Text;
            Filter();
        };
        Filter();
        return Framed(Card(Stack(Row(Segments(new[] { "Semua", "Aktif", "Nonaktif" }, startupFilter, v =>
        {
            startupFilter = v;
            Filter();
        }), SearchField("Cari", query)), Label("Menonaktifkan item startup tidak menghapus aplikasi apa pun dan dapat diaktifkan kembali kapan saja.", 12, false, "Muted"))), Card(table));
    }

    private UIElement Apps()
    {
        var table = Table("Aplikasi terpasang", ("Aplikasi", "Name", 190), ("Tipe", "Type", 75), ("Versi", "Version", 115), ("Publisher", "Publisher", 180));
        var query = SearchBox("Nama aplikasi atau publisher", appQuery);
        var pane = new ContentControl();
        void Detail()
        {
            if (inspected is not AppRow app)
            {
                pane.Content = Card(Label("Pilih aplikasi untuk melihat detail.", 14, false, "Muted"));
                return;
            }

            var residues = new List<Residue>();
            var details = Stack(Label(app.Type, 12, true, "Accent"), Label(app.Name, 20, true), Label(app.Publisher + "  ·  " + app.Version), Label(app.Location, 12, false, "Muted"), Label(app.Size + "  ·  " + app.InstallDate, 12, false, "Muted"), Label("Sisa file & registry", 16, true), ActionButton("Pindai leftover", () =>
            {
                scannedApps.Add(app);
                Detail();
            }));
            if (data.Apps.Contains(app))
            {
                var uninstall = ActionButton("Uninstall Aplikasi", () => Ask("Uninstall " + app.Name + "?", "Aplikasi akan dihapus dari daftar contoh.", "Mulai Uninstall", () =>
                {
                    lastUninstalled = app;
                    data.Apps.Remove(app);
                    Filter();
                    table.SelectedItem = null;
                    inspected = app;
                    Detail();
                    Sidebar();
                    Notify("Aplikasi contoh dihapus. Pilih Pindai leftover untuk memeriksa sisanya.");
                }), "danger");
                details.Children.Insert(5, uninstall);
            }

            if (scannedApps.Contains(app))
            {
                if (app.Leftovers.Count == 0)
                    details.Children.Add(Label("Tidak ada sisa file atau key registry.", 12, false, "Muted"));
                var remove = ActionButton("Hapus leftover terpilih", () => Ask("Hapus sisa yang dipilih?", residues.Count + " item contoh milik " + app.Name, "Hapus", () =>
                {
                    app.Leftovers.RemoveAll(residues.Contains);
                    Detail();
                    Notify("Leftover contoh dihapus.");
                }), "danger");
                remove.IsEnabled = false;
                foreach (var item in app.Leftovers)
                {
                    var box = Check("", false, v =>
                    {
                        if (v)
                            residues.Add(item);
                        else
                            residues.Remove(item);
                        remove.IsEnabled = residues.Count > 0;
                    });
                    box.Content = Label(item.Kind + " · " + item.Path, 12);
                    details.Children.Add(box);
                }

                details.Children.Add(remove);
            }

            pane.Content = Card(Scroll(details));
        }

        void Filter()
        {
            var items = data.Apps.Where(a => (appFilter == "Semua" || a.Type == appFilter) && Matches(a.Name + " " + a.Publisher, appQuery)).ToList();
            var next = items.Contains(inspected!) ? inspected : items.FirstOrDefault();
            table.ItemsSource = items;
            table.SelectedItem = next;
            inspected = next;
            Detail();
        }

        table.SelectionChanged += (_, _) =>
        {
            inspected = table.SelectedItem as AppRow;
            Detail();
        };
        query.TextChanged += (_, _) =>
        {
            appQuery = query.Text;
            Filter();
        };
        Filter();
        var controls = Row(SearchField("Cari aplikasi", query), Segments(new[] { "Semua", "Desktop", "Store" }, appFilter, v =>
        {
            appFilter = v;
            Filter();
        }));
        if (lastUninstalled != null)
            controls.Children.Add(ActionButton("Leftover uninstall terakhir", () =>
            {
                table.SelectedItem = null;
                inspected = lastUninstalled;
                Detail();
            }));
        return Framed(Card(controls), Split(Card(table), pane));
    }

    private readonly CacheRow downloads = new("Unduhan lebih dari 30 hari", 2, 28, "28 MB", @"C:\Contoh\Downloads", false)
    {
        Selected = false
    };
    private bool cleanedDownloads;
    private UIElement Cleanup()
    {
        var table = Table("Kategori pembersihan", ("Kategori", "Category", 215), ("File", "Files", 74), ("Ukuran", "SizeLabel", 95), ("Lokasi", "Path", 450));
        var selected = Label("", 14, true);
        var summary = Label("", 21, true);
        List<CacheRow> Visible() => data.Cleanup.Where(c => browserCache || !c.IsBrowser).Concat(oldDownloads && !cleanedDownloads ? new[] { downloads } : Array.Empty<CacheRow>()).ToList();
        var clean = ActionButton("Kirim Terpilih ke Recycle Bin", () =>
        {
            var rows = Visible().Where(x => x.Selected).ToList();
            if (rows.Count == 0)
                return;
            Ask("Kirim file terpilih ke Recycle Bin?", rows.Sum(x => x.Files).ToString("N0", PreviewData.Locale) + " file contoh · " + PreviewData.Size(rows.Sum(x => x.SizeMb)), "Kirim Terpilih", () =>
            {
                data.Cleanup.RemoveAll(rows.Contains);
                cleanedDownloads |= rows.Contains(downloads);
                Navigate("cleanup");
                Notify("File contoh dipindahkan ke Recycle Bin simulasi.");
            });
        }, "primary");
        void Total()
        {
            var rows = Visible();
            selected.Text = "Terpilih · " + PreviewData.Size(rows.Where(x => x.Selected).Sum(x => x.SizeMb));
            summary.Text = PreviewData.Size(rows.Sum(x => x.SizeMb)) + " siap dibersihkan";
            clean.IsEnabled = rows.Any(x => x.Selected);
        }

        var factory = new FrameworkElementFactory(typeof(CheckBox));
        factory.SetBinding(CheckBox.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        factory.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty, new Binding("Category"));
        factory.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler((_, _) => Total()));
        table.Columns.Insert(0, new DataGridTemplateColumn { Header = "Pilih", Width = 52, CellTemplate = new DataTemplate { VisualTree = factory } });
        void Filter()
        {
            table.ItemsSource = Visible();
            Total();
        }

        Filter();
        return Framed(Card(Stack(summary, Label("Centang kategori yang ingin dipindahkan ke Recycle Bin.", 13, false, "Muted"), Row(Check("Cache browser", browserCache, v =>
        {
            browserCache = v;
            Filter();
        }), Check("Analisis Downloads (>30 hari)", oldDownloads, v =>
        {
            oldDownloads = v;
            Filter();
        }), ActionButton("Scan Aman", () =>
        {
            Filter();
            Notify("Pemindaian data contoh selesai.");
        })))), Card(table), Card(Row(selected, clean)));
    }
}
