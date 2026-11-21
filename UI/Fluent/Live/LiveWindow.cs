using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ZidWindowsApps.Fluent;
internal sealed partial class LiveWindow : FluentWindow
{
    private readonly HashSet<string> ready = new(), loading = new();
    private readonly Dictionary<string, string> errors = new();
    private readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromSeconds(4)
    };
    private List<ProcessInfo> processes = new();
    private List<StartupItem> startup = new();
    private List<InstalledApp> apps = new();
    private List<CleanupItem> cache = new();
    private SystemSnapshot? system;
    private bool scanned, ticking, working, closed;
    private CancellationTokenSource? operation;
    private Action? updateProcesses, updateCpuCard;
    private int processCount, startupCount, startupTotal, memory;
    private double? cpu;
    private ulong previousTotal, previousIdle;
    private long free, capacity;
    private string drive = Path.GetPathRoot(Environment.SystemDirectory) ?? "";
    protected override bool Dark { get => App.Settings.DarkTheme; set => App.Settings.DarkTheme = value; }
    protected override bool Collapsed { get => App.Settings.SidebarCollapsed; set => App.Settings.SidebarCollapsed = value; }
    protected override bool AdminMode => Admin.IsAdministrator;

    protected override void SavePreferences()
    {
        if (!App.Settings.Save(out var error))
            Notify("Pengaturan gagal disimpan: " + error);
    }

    internal LiveWindow()
    {
        InitializeShell();
        showSystem = !App.Settings.HideSystemProcesses;
        var tab = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--tab=", StringComparison.Ordinal))?.Substring(6);
        Navigate(tab != null && IsRoute(tab) ? tab : "dashboard");
        timer.Tick += async (_, _) => await Tick();
        IsVisibleChanged += (_, _) => UpdateTimer();
        StateChanged += (_, _) => UpdateTimer();
        Closed += (_, _) =>
        {
            closed = true;
            timer.Stop();
            operation?.Cancel();
            App.ExitApplication();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.ExitRequested && (App.Settings.EnableTray || working))
        {
            e.Cancel = true;
            if (App.Settings.EnableTray)
            {
                Hide();
                App.ShowTrayHintOnce();
            }
            else
                Notify("Tunggu operasi selesai atau pilih Batalkan.");
        }

        base.OnClosing(e);
    }

    private void UpdateTimer()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
            timer.Start();
        else
            timer.Stop();
    }

    protected override void LeavingPage()
    {
        updateProcesses = null; updateCpuCard = null;
    }

    protected override void RequestAdmin()
    {
        if (AdminMode || working)
            return;
        Ask("Jalankan sebagai Administrator?", "Windows akan meminta izin dan membuka kembali halaman ini. Pilih lagi tindakan yang ingin dijalankan.", "Lanjutkan", () =>
        {
            try
            {
                var (file, args) = AppPaths.LaunchCommand("--elevated --tab=" + current);
                if (Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, Verb = "runas" }) != null)
                    App.ExitApplication();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Notify("Permintaan izin dibatalkan.");
            }
            catch (Exception ex)
            {
                Notify(ex.Message);
            }
        });
    }

    protected override string? Badge(string key) => key switch
    {
        "processes" when ready.Contains("dashboard") || ready.Contains(key) => processCount.ToString(),
        "autostart" when ready.Contains("dashboard") || ready.Contains(key) => startupCount.ToString(),
        "uninstaller" when ready.Contains(key) => apps.Count.ToString(),
        "cleanup" when scanned => Format.Bytes(cache.Sum(x => x.Size)),
        _ => null
    };
    private void Render()
    {
        if (!closed)
        {
            Get<ContentControl>("Viewport").Content = Page(current);
            Sidebar();
        }
    }

    protected override void RefreshPage()
    {
        if (working || loading.Contains(current))
            return;
        if (current == "cleanup")
        {
            ScanCache();
            return;
        }

        ready.Clear();
        errors.Clear();
        Render();
    }

    protected override UIElement Page(string key)
    {
        if (errors.TryGetValue(key, out var error))
            return Card(Stack(Label("Gagal memuat halaman", 17, true), Label(error), ActionButton("Coba lagi", RefreshPage)));
        if (key is "dashboard" or "processes" or "autostart" or "uninstaller" or "sysinfo" or "tools" && !ready.Contains(key))
        {
            if (loading.Add(key))
                Dispatcher.BeginInvoke(new Action(() => _ = LoadPage(key)));
            return Card(Stack(Label("Membaca data Windows…", 15, true), new ProgressBar { IsIndeterminate = true, Height = 4 }));
        }

        return key switch
        {
            "processes" => Processes(),
            "autostart" => Startup(),
            "uninstaller" => AppsPage(),
            "cleanup" => Cleanup(),
            "sysinfo" => SystemInfo(),
            "troubleshooting" => Diagnostics(),
            "tools" => ToolsPage(),
            "settings" => SettingsPage(),
            _ => Dashboard()
        };
    }

    private async Task LoadPage(string key)
    {
        try
        {
            switch (key)
            {
                case "dashboard":
                    var values = await Task.Run(() =>
                    {
                        var p = Process.GetProcesses();
                        int n = p.Length;
                        foreach (var item in p)
                            item.Dispose();
                        return (n, StartupService.GetItems(false));
                    });
                    processCount = values.n;
                    if (!ready.Contains("autostart"))
                    {
                        startupCount = values.Item2.Count(s => s.IsEnabled);
                        startupTotal = values.Item2.Count;
                    }

                    MeasureTelemetry();
                    break;
                case "processes":
                    processes = await Task.Run(() => ProcessService.GetProcesses(true));
                    processCount = processes.Count;
                    break;
                case "autostart":
                    startup = await Task.Run(() => StartupService.GetItems());
                    startupCount = startup.Count(s => s.IsEnabled);
                    startupTotal = startup.Count;
                    break;
                case "uninstaller":
                    apps = await Task.Run(InstalledAppsService.GetInstalledApps);
                    break;
                case "sysinfo":
                    system = await Task.Run(SystemInfoService.GetSnapshotAsync);
                    break;
                case "tools":
                    await ReadPowerPlans();
                    break;
            }

            ready.Add(key);
        }
        catch (Exception ex)
        {
            errors[key] = ex.Message;
            App.Logger.Write("Gagal memuat " + key + ": " + ex.Message);
        }
        finally
        {
            loading.Remove(key);
            if (!closed)
            {
                if (current == key)
                    Render();
                else
                    Sidebar();
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    private void MeasureTelemetry()
    {
        memory = SystemInfoService.GetMemoryLoadPercent();
        var disk = new DriveInfo(drive);
        free = disk.AvailableFreeSpace;
        capacity = disk.TotalSize;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong total = kernel + user;
            if (previousTotal > 0 && total > previousTotal)
                cpu = Math.Clamp(100.0 * (1.0 - (idle - previousIdle) / (double)(total - previousTotal)), 0, 100);
            previousTotal = total;
            previousIdle = idle;
        }

        updateCpuCard?.Invoke();
        Get<TextBlock>("Telemetry").Text = $"CPU {cpu?.ToString("0") ?? "–"}%    RAM {memory}%    {drive.TrimEnd('\\')} {Format.Bytes(free)} bebas";
    }

    private async Task Tick()
    {
        if (ticking || working || !IsVisible || Get<Grid>("Overlay").IsVisible)
            return;
        ticking = true;
        try
        {
            MeasureTelemetry();
            if (current == "processes" && ready.Contains(current) && !loading.Contains(current))
            {
                var rows = await Task.Run(() => ProcessService.GetProcesses(true));
                if (current == "processes" && !working && !Get<Grid>("Overlay").IsVisible && !closed)
                {
                    processes = rows;
                    processCount = rows.Count;
                    updateProcesses?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            if (!closed)
                Get<TextBlock>("Status").Text = "Telemetri tidak tersedia: " + ex.Message;
        }
        finally
        {
            ticking = false;
        }
    }

    private async Task Work(string title, Func<CancellationToken, IProgress<string>, Task<string>> action, bool cancel = false)
    {
        if (working)
            return;
        working = true;
        using var source = new CancellationTokenSource();
        operation = source;
        var detail = Label(title);
        var button = ActionButton("Batalkan", () => source.Cancel());
        button.Visibility = cancel ? Visibility.Visible : Visibility.Collapsed;
        var block = Stack(Label(title, 17, true), new ProgressBar { IsIndeterminate = true, Height = 4 }, detail, button);
        OpenOverlay(block, button);
        Get<TextBlock>("Status").Text = title;
        var latest = new LatestProgress();
        var progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        progressTimer.Tick += (_, _) =>
        {
            if (latest.Take() is string message)
                detail.Text = message;
        };
        progressTimer.Start();
        try
        {
            var result = await action(source.Token, latest);
            if (!closed)
            {
                App.Logger.Write(title + ": " + result);
                Notify(result);
            }
        }
        catch (OperationCanceledException)
        {
            if (!closed)
                Notify("Operasi dibatalkan.");
        }
        catch (Exception ex)
        {
            if (!closed)
            {
                App.Logger.Write(title + ": " + ex.Message);
                Notify("Gagal: " + ex.Message);
            }
        }
        finally
        {
            progressTimer.Stop();
            working = false;
            operation = null;
            if (!closed)
            {
                CloseOverlay();
                Render();
            }
        }
    }

    private sealed class LatestProgress : IProgress<string>
    {
        private string? latest;
        public void Report(string value) => Interlocked.Exchange(ref latest, value);
        public string? Take() => Interlocked.Exchange(ref latest, null);
    }

    internal bool HasOperation => working;

    internal void ShowOperation()
    {
        Show();
        Activate();
        Notify("Tunggu operasi selesai atau pilih Batalkan.");
    }

    protected override bool CanDismissOverlay => !working;

    private void ConfirmWork(string title, string detail, string label, Func<CancellationToken, IProgress<string>, Task<string>> action, bool cancel = false) => Ask(title, detail, label, () => _ = Work(title, action, cancel));
}
