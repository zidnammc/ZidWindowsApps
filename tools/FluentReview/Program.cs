using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZidWindowsApps;

static class Review
{
    static readonly string[] keys =
    {
        "dashboard",
        "processes",
        "autostart",
        "uninstaller",
        "cleanup",
        "sysinfo",
        "troubleshooting",
        "tools",
        "settings"
    };
    static object? Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);
    static IEnumerable<T> Find<T>(DependencyObject p)
        where T : DependencyObject
    {
        if (p is T x)
            yield return x;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++)
            foreach (var c in Find<T>(VisualTreeHelper.GetChild(p, i)))
                yield return c;
    }

    static void Update(Window w)
    {
        w.UpdateLayout();
        w.Dispatcher.Invoke(() =>
        {
        }, DispatcherPriority.ApplicationIdle);
    }

    static void Capture(Window w, string path)
    {
        Update(w);
        var root = (FrameworkElement)w.Content;
        var img = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        img.Render(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(img));
        using var file = File.Create(path);
        png.Save(file);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    static Button Button(Window w, string name) => Find<Button>(w).First(b => b.IsVisible && (Equals(b.Content, name) || AutomationProperties.GetName(b) == name));
    private static void CheckFixtures(Window w)
    {
        var root = Path.Combine(Path.GetTempPath(), "ZidWindowsApps-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Path.Combine(root, "original.txt"); var copy = Path.Combine(root, "copy.txt"); var report = Path.Combine(root, "report.txt");
        try
        {
            File.WriteAllText(original, "ZidWindowsApps fixture"); File.Copy(original, copy);
            var group = DuplicateService.Find(root, null, default).Single();
            group.Files.Sort((a, b) => a == original ? -1 : b == original ? 1 : StringComparer.Ordinal.Compare(a, b));
            var folder = w.GetType().GetField("folder", BindingFlags.NonPublic | BindingFlags.Instance)!;
            folder.SetValue(w, root);
            var selection = new List<(DuplicateGroup, string)> { (group, copy) };
            File.AppendAllText(copy, "changed");
            Call(w, "RecycleCopies", selection); Assert(File.Exists(copy), "Changed copy recycled");
            File.Copy(original, copy, true);
            Call(w, "RecycleCopies", selection); Assert(File.Exists(original) && !File.Exists(copy), "Original/copy recycling");
            var settings = new AppSettings { ExcludedPaths = new List<string> { root } };
            var category = new CleanupItem { Category = "Fixture", Files = new List<string> { original }, Path = root };
            var skipped = new CleanupService(settings).Delete(new[] { category }, false, null);
            Assert(skipped.Skipped == 1 && File.Exists(original), "Cleanup exclusions");
            SystemInfoService.ExportReportAsync(new SystemSnapshot { ComputerName = "FIXTURE" }, report).GetAwaiter().GetResult();
            Assert(File.ReadAllText(report).Contains("FIXTURE"), "Export report");
            var info = new ProcessInfo { Id = Environment.ProcessId, Name = "FluentReview" };
            try { ProcessService.Kill(info, true); throw new Exception("Self protection missing"); }
            catch (InvalidOperationException ex) { Assert(ex.Message.Contains("sendiri"), "Self protection"); }
            var fixtureKey = @"Software\ZidWindowsAppsTest-" + Guid.NewGuid().ToString("N");
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(fixtureKey)) key.SetValue("Marker", "FluentReview");
            try
            {
                Assert(InstalledAppsService.TryRemoveLeftover(new LeftoverItem { Kind = LeftoverKind.Registry, Target = fixtureKey }, out var error), "Fixture registry backup: " + error);
                using var removed = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(fixtureKey);
                Assert(removed == null, "Fixture registry removal");
                var file = Directory.GetFiles(AppPaths.RegistryBackupDirectory, "*" + fixtureKey.Split('\\')[1] + "*.reg").Single();
                Assert(File.ReadAllText(file).Contains("FluentReview"), "Invalid fixture .reg");
                File.Delete(file);
            }
            finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(fixtureKey, false); }
            Console.WriteLine("PASS fixture registry export and removal");
            Console.WriteLine("PASS fixtures: changed copy, original retained, recycle copy, excluded cleanup, TXT export, self protection");
        }
        finally { foreach (var path in new[] { original, copy, report }) if (File.Exists(path)) File.Delete(path); Directory.Delete(root); }
    }
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 2 || (args[1] != "before" && args[1] != "preview" && args[1] != "live"))
        {
            Console.Error.WriteLine("FluentReview <output-directory> before|preview|live");
            return 2;
        }

        string output = args[0];
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        if (args[1] == "live")
        {
            var type = typeof(MainWindow).Assembly.GetType("ZidWindowsApps.Fluent.LiveWindow", true)!;
            var w = (Window)Activator.CreateInstance(type, true)!;
            w.ShowActivated = false; w.ShowInTaskbar = false; w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -20000; w.Show();
            var root = (FrameworkElement)w.Content;
            void WaitReady(string route)
            {
                var clock = Stopwatch.StartNew();
                var loading = (HashSet<string>)type.GetField("loading", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(w)!;
                do { Update(w); Thread.Sleep(50); } while (loading.Contains(route) && clock.Elapsed.TotalSeconds < 150);
                Assert(!loading.Contains(route), "Timeout " + route);
                Assert(!Find<TextBlock>(w).Any(t => t.Text == "Gagal memuat halaman"), "Error page " + route);
            }
            foreach (var route in keys)
            {
                Call(w, "Navigate", route); WaitReady(route);
                Assert(!Find<TextBlock>(w).Any(t => t.Text.Contains("Pratinjau · Data contoh")), "Fixture in live " + route);
                Capture(w, Path.Combine(output, "live", route + ".png"));
                if (route == "dashboard") Assert(((TextBlock)root.FindName("Telemetry")).Text.Contains("RAM "), "Missing telemetry");
                if (route == "processes")
                {
                    var grid = Find<DataGrid>(w).Single(); Assert(grid.Items.Count > 0, "No processes");
                    var query = Find<TextBox>(w).First(t => AutomationProperties.GetName(t) == "Nama, PID, publisher");
                    query.Text = "no-such-process-ZidFluentQA"; Assert(grid.Items.Count == 0, "Process filter"); query.Text = "";
                }
                if (route == "tools") Assert(Find<Button>(w).Any(b => Equals(b.Content, "Pindai duplikat")), "Missing duplicate scan");
                if (route == "tools" && Find<Button>(w).Any(b => Equals(b.Content, "Balanced"))) Assert(!Find<TextBlock>(w).Any(t => t.Text == "Rencana aktif · Tidak terbaca"), "Active power plan enumeration");
                if (route == "settings") { Click(Button(w, "Hapus semua log aktivitas")); Update(w); Click(Button(w, "Batal")); Update(w); }
                Console.WriteLine("PASS live " + route);
            }
            bool oldTheme = App.Settings.DarkTheme;
            App.Settings.DarkTheme = true;
            Call(w, "SetColors");
            foreach (var route in keys) { Call(w, "Navigate", route); WaitReady(route); Capture(w, Path.Combine(output, "live-dark", route + ".png")); }
            App.Settings.DarkTheme = oldTheme;
            Call(w, "SetColors"); Call(w, "ShowPalette"); Update(w); Click(Button(w, "Tutup")); CheckFixtures(w); w.Hide();
        }
        else if (args[1] == "before")
        {
            app.Resources.MergedDictionaries.Add(App.LoadXaml<ResourceDictionary>("Styles.xaml"));
            var w = new MainWindow();
            w.ShowActivated = false;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = -20000;
            w.Show();
            var tabs = (TabControl)((FrameworkElement)w.Content).FindName("MainTabs");
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 8)
            {
                Update(w);
                Thread.Sleep(50);
            }

            for (int i = 0; i < keys.Length; i++)
            {
                tabs.SelectedIndex = i;
                Capture(w, Path.Combine(output, "before", keys[i] + ".png"));
            }
        }
        else
        {
            var type = typeof(MainWindow).Assembly.GetType("ZidWindowsApps.Fluent.PreviewWindow", true)!;
            var w = (Window)Activator.CreateInstance(type, true)!;
            w.ShowActivated = false;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = -20000;
            w.Width = 1280;
            w.Height = 720;
            w.Show();
            var root = (FrameworkElement)w.Content;
            var prefs = type.GetField("prefs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
            foreach (var theme in new[]
            {
                "light",
                "dark"
            }

            )
            {
                prefs.GetType().GetProperty("Dark")!.SetValue(prefs, theme == "dark");
                Call(w, "SetColors");
                foreach (var route in keys)
                {
                    Call(w, "Navigate", route);
                    Capture(w, Path.Combine(output, "preview", theme, route + ".png"));
                }
            }

            Call(w, "Navigate", "processes");
            Update(w);
            var table = Find<DataGrid>(w).Single();
            int start = table.Items.Count;
            var query = Find<TextBox>(w).First(t => AutomationProperties.GetName(t) == "Nama, PID, publisher");
            query.Text = "9132";
            Assert(table.Items.Count == 1, "Filter PID");
            query.Text = "";
            table.SelectedIndex = 0;
            Click(Button(w, "Hentikan Proses"));
            Update(w);
            Capture(w, Path.Combine(output, "preview", "dialog.png"));
            Click(Button(w, "Batal"));
            Assert(table.Items.Count == start, "Cancel mutated fixture");
            Click(Button(w, "Hentikan Proses"));
            Update(w);
            Click(Button(w, "Hentikan"));
            Assert(table.Items.Count == start - 1, "Confirm did not mutate fixture");
            Call(w, "ShowPalette");
            Update(w);
            var palette = Find<TextBox>(w).First(t => AutomationProperties.GetName(t) == "Cari modul");
            palette.Text = "settings";
            Assert(Find<ListBox>(w).Any(l => l.IsVisible && l.Items.Count == 1), "Palette filter");
            Capture(w, Path.Combine(output, "preview", "palette.png"));
            Call(w, "CloseOverlay");
            Call(w, "Navigate", "cleanup");
            Update(w);
            var cleanup = Find<DataGrid>(w).Single();
            int categories = cleanup.Items.Count;
            var cache = Find<CheckBox>(w).First(x => Equals(x.Content, "Cache browser"));
            cache.IsChecked = false;
            cache.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            Assert(cleanup.Items.Count == categories - 2, "Browser filter");
            Call(w, "Navigate", "uninstaller");
            Update(w);
            Click(Button(w, "Pindai leftover"));
            Update(w);
            Assert(Find<Button>(w).Any(b => Equals(b.Content, "Hapus leftover terpilih") && !b.IsEnabled), "Leftover selected by default");
            Click(Button(w, "Uninstall Aplikasi"));
            Update(w);
            Click(Button(w, "Mulai Uninstall"));
            Update(w);
            Assert(Find<DataGrid>(w).Single().Items.Count == 11, "Uninstall fixture");
            Click(Button(w, "Pindai leftover"));
            Update(w);
            Assert(Find<Button>(w).Any(b => Equals(b.Content, "Hapus leftover terpilih")), "Post-uninstall scan");
            Call(w, "Navigate", "autostart");
            Update(w);
            var startup = Find<CheckBox>(w).First(b => AutomationProperties.GetName(b).StartsWith("GoogleChromeAutoLaunch"));
            startup.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            Update(w);
            Click(Button(w, "Ubah status"));
            Update(w);
            Assert(Find<CheckBox>(w).First(b => AutomationProperties.GetName(b).StartsWith("GoogleChromeAutoLaunch")).IsChecked == true, "Startup toggle");
            Call(w, "ShowGallery");
            Update(w);
            Click(Button(w, "Gagal"));
            Update(w);
            Click(Button(w, "Coba lagi"));
            Update(w);
            Capture(w, Path.Combine(output, "preview", "gallery.png"));
            Click(Button(w, "Tutup"));
            Call(w, "Navigate", "sysinfo");
            Update(w);
            Click(Button(w, "Raw TXT Report"));
            Update(w);
            Assert(Find<TextBox>(w).Any(b => b.Text.Contains("LAPORAN CONTOH")), "Raw report");
            Call(w, "Navigate", "troubleshooting");
            Update(w);
            Click(Button(w, "Jalankan pemeriksaan"));
            Update(w);
            Click(Button(w, "Jalankan"));
            Update(w);
            Click(Button(w, "Batalkan"));
            var wait = Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < 3000 && !Find<TextBox>(w).Any(t => t.Text.Contains("dibatalkan")))
            {
                Update(w);
                Thread.Sleep(25);
            }

            Assert(Find<TextBox>(w).Any(t => t.Text.Contains("dibatalkan")), "Cancel diagnostic");
            prefs.GetType().GetProperty("Collapsed")!.SetValue(prefs, true);
            Call(w, "Sidebar");
            w.Width = 1024;
            w.Height = 640;
            Call(w, "Navigate", "dashboard");
            Capture(w, Path.Combine(output, "preview", "compact.png"));
            w.Width = 1366;
            w.Height = 768;
            Capture(w, Path.Combine(output, "preview", "wide.png"));
        }

        Console.WriteLine("PASS " + args[1]);
        return 0;
    }
}
