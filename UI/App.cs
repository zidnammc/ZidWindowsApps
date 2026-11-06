using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace ZidWindowsApps;

public sealed class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static ActivityLogger Logger { get; private set; } = new();
    public static bool ExitRequested { get; set; }

    private static WinForms.NotifyIcon? _tray;
    private EventWaitHandle? _activateEvent;
    private bool _trayHintShown;

    public App(AppSettings settings, ActivityLogger logger)
    {
        Settings = settings;
        Logger = logger;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;   // tray: window bisa ditutup tanpa menghentikan aplikasi
    }

    /// <summary>Memuat file XAML yang tertanam di assembly (UI dibaca saat runtime, bukan dikompilasi ke BAML).</summary>
    public static T LoadXaml<T>(string resourceName) where T : class
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ZidWindowsApps.Resources.{resourceName}")
                           ?? throw new FileNotFoundException("Resource tidak ditemukan: " + resourceName);
        return (T)XamlReader.Load(stream);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Write("Error tak tertangani: " + args.Exception);
            MessageBox.Show("Terjadi kesalahan tak terduga:\n\n" + args.Exception.Message + "\n\nDetail dicatat di log aktivitas.",
                "ZidWindowsApps", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        SessionEnding += (_, _) => ExitRequested = true;

        Logger.CleanupOldLogs(Settings.LogRetentionDays);

        try
        {
            Resources.MergedDictionaries.Add(LoadXaml<ResourceDictionary>("Styles.xaml"));
            var window = new MainWindow();
            MainWindow = window;
            ConfigureTray();
            ListenForActivation();
            window.Show();
        }
        catch (Exception ex)
        {
            Logger.Write("Gagal memulai: " + ex);
            MessageBox.Show("ZidWindowsApps gagal dimulai:\n\n" + ex.Message + "\n\nDetail ada di:\n" + AppPaths.LogsDirectory,
                "ZidWindowsApps", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitRequested = true;
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _tray = null;
        _activateEvent?.Dispose();
        base.OnExit(e);
    }

    private void ListenForActivation()
    {
        try
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ActivateEventName);
            var thread = new Thread(() =>
            {
                while (_activateEvent != null)
                {
                    try { _activateEvent.WaitOne(); } catch { return; }
                    Dispatcher.BeginInvoke(new Action(ShowMainWindow));
                }
            }) { IsBackground = true, Name = "ActivationListener" };
            thread.Start();
        }
        catch { }
    }

    public static void ConfigureTray()
    {
        if (!Settings.EnableTray)
        {
            _tray?.Dispose();
            _tray = null;
            return;
        }
        if (_tray != null) return;

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Buka ZidWindowsApps", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Keluar", null, (_, _) => ExitApplication());

        _tray = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "ZidWindowsApps",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowMainWindow();
    }

    public static void ShowTrayHintOnce()
    {
        if (Current is App app && !app._trayHintShown && _tray != null)
        {
            app._trayHintShown = true;
            _tray.ShowBalloonTip(3000, "ZidWindowsApps", "Aplikasi tetap berjalan di system tray. Klik kanan ikon > Keluar untuk menutup.", WinForms.ToolTipIcon.Info);
        }
    }

    public static void ShowMainWindow()
    {
        var w = Current?.MainWindow;
        if (w == null) return;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
    }

    public static void ExitApplication()
    {
        ExitRequested = true;
        Current?.Dispatcher.Invoke(() => Current.Shutdown());
    }
}
