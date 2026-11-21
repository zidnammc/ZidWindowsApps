using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZidWindowsApps;

/// <summary>
/// Jendela utama. UI dibaca dari Resources/MainWindow.xaml saat runtime; semua event dipasang di BindEvents()
/// supaya tidak ada nama handler yang "yatim" antara XAML dan C#.
/// File ini: inisialisasi, navigasi, dashboard, pengaturan. Tab lain ada di MainWindow.*.cs.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly FrameworkElement _root;
    private readonly DispatcherTimer _processTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _loadingSettings;
    private bool _refreshingDashboard;
    private bool _appliedScheduleEnabled;
    private string _appliedScheduleFrequency = "";

    public MainWindow()
    {
        Title = "ZidWindowsApps";
        Width = 1100;
        Height = 760;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = (Brush)Application.Current.Resources["PageBrush"];

        _root = App.LoadXaml<FrameworkElement>("MainWindow.xaml");
        Content = _root;

        BindControls();
        BindEvents();
        InitializeState();
    }

    private T F<T>(string name) where T : class =>
        _root.FindName(name) as T ?? throw new InvalidOperationException($"Kontrol '{name}' tidak ada di MainWindow.xaml.");

    // ------------------------------------------------------------------ inisialisasi

    private void InitializeState()
    {
        var admin = Admin.IsAdministrator;
        AdminStatus.Text = admin ? "ADMINISTRATOR" : "PENGGUNA BIASA";
        AdminStatus.Foreground = new SolidColorBrush(admin ? Color.FromRgb(0x7F, 0xE3, 0xA1) : Color.FromRgb(0xF5, 0xC2, 0x6B));
        AdminStatus.ToolTip = admin ? "Berjalan dengan hak Administrator." : "Beberapa fitur (registry sistem, layanan, SFC) butuh Administrator.";
        ElevateButton.Visibility = admin ? Visibility.Collapsed : Visibility.Visible;

        _loadingSettings = true;
        LoadSettingsIntoUi();
        _loadingSettings = false;
        _appliedScheduleEnabled = App.Settings.ScheduledCleanupEnabled;
        _appliedScheduleFrequency = App.Settings.CleanupFrequency;

        TroubleList.ItemsSource = TroubleshootingService.GetItems();
        ApplyLanguage();

        _processTimer.Tick += async (_, _) => await RefreshProcessesAsync(showError: false);

        Loaded += async (_, _) =>
        {
            await RefreshDashboardAsync();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Saat tray aktif, tombol X hanya menyembunyikan window (dulu: tray ikut hilang dan aplikasi berhenti).
        if (App.Settings.EnableTray && !App.ExitRequested)
        {
            e.Cancel = true;
            Hide();
            UpdateProcessTimer();
            App.ShowTrayHintOnce();
            return;
        }
        _processTimer.Stop();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.ExitApplication();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateProcessTimer();
    }

    // ------------------------------------------------------------------ helper umum

    private void SetBusy(string text, bool busy)
    {
        ProgressText.Text = text;
        OperationProgress.Visibility = busy ? Visibility.Visible : Visibility.Hidden;
        OperationProgress.IsIndeterminate = busy;
    }

    /// <summary>
    /// Konfirmasi tindakan. force=true untuk tindakan berisiko tinggi (hapus registry, matikan layanan, uninstall):
    /// tetap bertanya walaupun pengguna mematikan konfirmasi di Pengaturan.
    /// </summary>
    private bool ConfirmAction(string message, bool force = false)
    {
        if (!force && !App.Settings.ConfirmBeforeAction) return true;
        return MessageBox.Show(this, message, "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void Info(string message) =>
        MessageBox.Show(this, message, "ZidWindowsApps", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowError(string action, Exception ex)
    {
        App.Logger.Write($"{action} gagal: {ex.Message}");
        MessageBox.Show(this, $"{action} tidak berhasil.\n\n{ex.Message}", "ZidWindowsApps", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    // ------------------------------------------------------------------ navigasi

    private void Navigate(TabItem tab)
    {
        MainTabs.SelectedItem = tab;
        if (tab == StartupTab && _allStartup.Count == 0) _ = RefreshStartupAsync();
        if (tab == AppsTab && _allApps.Count == 0) _ = RefreshAppsAsync();
        if (tab == ProcessesTab) _ = RefreshProcessesAsync(showError: true);
        UpdateProcessTimer();
    }

    /// <summary>Polling proses hanya berjalan saat tab Proses terlihat (dulu terus berjalan walau window disembunyikan).</summary>
    private void UpdateProcessTimer()
    {
        bool shouldRun = IsVisible && WindowState != WindowState.Minimized && MainTabs.SelectedItem == ProcessesTab;
        if (shouldRun && !_processTimer.IsEnabled) _processTimer.Start();
        else if (!shouldRun && _processTimer.IsEnabled) _processTimer.Stop();
    }

    private void ToggleSidebar()
    {
        bool hide = SidebarColumn.Width.Value > 0;
        SidebarColumn.Width = new GridLength(hide ? 0 : 170);
        Sidebar.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ElevateRestart()
    {
        if (!ConfirmAction("Aplikasi akan dijalankan ulang dengan hak Administrator. Lanjutkan?", force: true)) return;
        try
        {
            var (file, args) = AppPaths.LaunchCommand("--elevated");
            Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, Verb = "runas" });
            App.ExitApplication();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Info("Permintaan izin Administrator dibatalkan.");
        }
        catch (Exception ex) { ShowError("Menjalankan ulang sebagai Administrator", ex); }
    }

    // ------------------------------------------------------------------ dashboard

    private async Task RefreshDashboardAsync()
    {
        if (_refreshingDashboard) return;
        _refreshingDashboard = true;
        try
        {
            SetBusy("Memeriksa kondisi perangkat...", true);
            var data = await Task.Run(() =>
            {
                var processCount = ProcessService.GetProcesses(true).Count;                // satu kali saja (dulu dua kali)
                var startup = StartupService.GetItems(includeServices: false).Count(s => s.IsEnabled); // tanpa PowerShell
                var drives = SystemInfoService.GetFixedDrives();
                var memory = SystemInfoService.GetMemoryLoadPercent();
                return (processCount, startup, drives, memory);
            });

            ProcessCountValue.Text = data.processCount.ToString();
            StartupCountValue.Text = data.startup.ToString();

            long free = data.drives.Sum(d => d.Free);
            FreeSpaceValue.Text = data.drives.Count == 0 ? "-" : Format.Bytes(free);
            FreeSpaceValue.ToolTip = string.Join(Environment.NewLine, data.drives.Select(d => $"{d.Name}  {Format.Bytes(d.Free)} kosong dari {Format.Bytes(d.Total)}"));

            // Skor heuristik sederhana dari kondisi nyata (memori, disk sistem, jumlah auto-start, jumlah proses).
            int score = 100;
            if (data.memory > 85) score -= 15; else if (data.memory > 70) score -= 8;
            var sys = data.drives.FirstOrDefault(d => d.Name.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows)[..3], StringComparison.OrdinalIgnoreCase));
            if (sys.Total > 0)
            {
                double pct = sys.Free * 100.0 / sys.Total;
                if (pct < 10) score -= 20; else if (pct < 20) score -= 8;
            }
            if (data.startup > 25) score -= 10; else if (data.startup > 15) score -= 5;
            if (data.processCount > 250) score -= 5;
            score = Math.Clamp(score, 0, 100);
            HealthValue.Text = $"{score}/100";
            HealthValue.ToolTip = $"Memori terpakai {data.memory}%, {data.startup} item auto-start aktif, {data.processCount} proses. Skor ini hanya perkiraan kasar.";
            SetBusy(score >= 85 ? "Perangkat dalam kondisi baik." : score >= 65 ? "Perangkat cukup baik." : "Perangkat perlu perhatian.", false);
        }
        catch (Exception ex)
        {
            HealthValue.Text = "?";
            SetBusy("Gagal membaca kondisi perangkat: " + ex.Message, false);
            App.Logger.Write("Dashboard gagal: " + ex.Message);
        }
        finally { _refreshingDashboard = false; }
    }

    // ------------------------------------------------------------------ pengaturan

    private void LoadSettingsIntoUi()
    {
        var s = App.Settings;
        LanguageBox.SelectedIndex = s.Language.Equals("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TrayBox.IsChecked = s.EnableTray;
        HideSystemBox.IsChecked = s.HideSystemProcesses;
        ConfirmBox.IsChecked = s.ConfirmBeforeAction;
        ScheduledCleanupBox.IsChecked = s.ScheduledCleanupEnabled;
        ShowSystemProcesses.IsChecked = !s.HideSystemProcesses;
        CleanupFrequencyBox.SelectedIndex = s.CleanupFrequency switch { "Harian" or "Daily" => 0, "Bulanan" or "Monthly" => 2, _ => 1 };
        LogRetentionText.Text = $"Log aktivitas disimpan selama {s.LogRetentionDays} hari.";
        SettingsPathText.Text = $"Pengaturan: {AppPaths.SettingsFile}\nLog: {AppPaths.LogsDirectory}\nCadangan registry: {AppPaths.RegistryBackupDirectory}\nPengecualian folder: {s.ExcludedPaths.Count}";
    }

    private bool ReadSettingsFromUi()
    {
        var s = App.Settings;
        s.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "id";
        s.EnableTray = TrayBox.IsChecked == true;
        s.HideSystemProcesses = HideSystemBox.IsChecked == true;
        s.ConfirmBeforeAction = ConfirmBox.IsChecked == true;
        s.ScheduledCleanupEnabled = ScheduledCleanupBox.IsChecked == true;
        s.CleanupFrequency = (CleanupFrequencyBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mingguan";
        return true;
    }

    /// <summary>Menyimpan pengaturan; jadwal pembersihan hanya diubah bila memang berubah dan dijalankan di background.</summary>
    private async Task SaveAndApplySettingsAsync(bool announce)
    {
        ReadSettingsFromUi();
        var s = App.Settings;

        if (!s.Save(out var error))
        {
            MessageBox.Show(this, "Pengaturan tidak dapat disimpan: " + error, "ZidWindowsApps", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        App.ConfigureTray();

        bool scheduleChanged = s.ScheduledCleanupEnabled != _appliedScheduleEnabled ||
                               (s.ScheduledCleanupEnabled && !s.CleanupFrequency.Equals(_appliedScheduleFrequency, StringComparison.OrdinalIgnoreCase));
        string message = announce ? "Pengaturan tersimpan." : "";

        if (scheduleChanged)
        {
            SetBusy("Mengatur jadwal pembersihan...", true);
            var (ok, text) = await Task.Run(() => ScheduledCleanupService.Configure(s.ScheduledCleanupEnabled, s.CleanupFrequency));
            SetBusy(text, false);
            App.Logger.Write(text);
            if (ok)
            {
                _appliedScheduleEnabled = s.ScheduledCleanupEnabled;
                _appliedScheduleFrequency = s.CleanupFrequency;
                message = text;
            }
            else
            {
                // Jangan berpura-pura sukses: kembalikan pilihan ke keadaan sebenarnya.
                s.ScheduledCleanupEnabled = _appliedScheduleEnabled;
                _loadingSettings = true;
                ScheduledCleanupBox.IsChecked = _appliedScheduleEnabled;
                _loadingSettings = false;
                s.Save(out _);
                MessageBox.Show(this, text + (Admin.IsAdministrator ? "" : "\n\nCoba jalankan aplikasi sebagai Administrator."), "Jadwal pembersihan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (announce && message.Length > 0) Info(message);
    }

    private async void OnSettingChanged()
    {
        if (_loadingSettings || !IsLoaded) return;
        await SaveAndApplySettingsAsync(announce: false);
    }

    private void ManageExclusions()
    {
        var dialog = new InputDialog("Masukkan folder yang tidak boleh disentuh pembersihan:", this);
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        var path = PathGuard.Normalize(dialog.Value);
        if (!System.IO.Directory.Exists(path)) { Info("Folder tidak ditemukan."); return; }
        if (App.Settings.ExcludedPaths.Any(p => PathGuard.Normalize(p).Equals(path, StringComparison.OrdinalIgnoreCase))) { Info("Folder itu sudah ada di daftar pengecualian."); return; }

        App.Settings.ExcludedPaths.Add(path);
        App.Settings.Save(out _);
        LoadSettingsIntoUi();
        Info("Folder ditambahkan ke pengecualian.");
    }

    private void ClearLogs()
    {
        if (!ConfirmAction("Hapus semua log aktivitas?")) return;
        Info(App.Logger.ClearAll() ? "Log telah dihapus." : "Sebagian log tidak dapat dihapus.");
    }

    private static readonly Dictionary<string, (string Id, string En)> Labels = new()
    {
        ["NavDashboard"] = ("Dashboard", "Dashboard"),
        ["NavProcesses"] = ("Proses", "Processes"),
        ["NavStartup"] = ("Auto-start", "Auto-start"),
        ["NavApps"] = ("Uninstaller", "Uninstaller"),
        ["NavCleanup"] = ("Pembersihan", "Cleanup"),
        ["NavSystem"] = ("Spesifikasi", "System info"),
        ["NavTroubleshoot"] = ("Troubleshooting", "Troubleshooting"),
        ["NavTools"] = ("Tools", "Tools"),
        ["NavSettings"] = ("Pengaturan", "Settings"),
    };

    private void ApplyLanguage()
    {
        bool en = App.Settings.Language.Equals("en", StringComparison.OrdinalIgnoreCase);
        foreach (var (name, text) in Labels)
            F<Button>(name).Content = en ? text.En : text.Id;

        AppSubtitle.Text = en ? "A simple and safe Windows maintenance center" : "Pusat perawatan Windows yang mudah dan aman";
        WelcomeText.Text = en ? "Welcome to ZidWindowsApps" : "Selamat datang di ZidWindowsApps";
        WelcomeHint.Text = en ? "Use the menu on the left to inspect and maintain Windows." : "Gunakan menu di sebelah kiri untuk memeriksa dan merawat Windows.";
        SettingsTitle.Text = en ? "ZidWindowsApps settings" : "Pengaturan ZidWindowsApps";
    }
}
