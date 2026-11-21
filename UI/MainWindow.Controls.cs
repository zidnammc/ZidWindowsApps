using System.Windows;
using System.Windows.Controls;

namespace ZidWindowsApps;

// Referensi kontrol dan pemasangan event. Setiap nama di sini HARUS ada sebagai x:Name di Resources/MainWindow.xaml.
public sealed partial class MainWindow
{
    // header & status
    private Button SidebarToggleButton = null!, ElevateButton = null!, RefreshDashboardButton = null!;
    private TextBlock AppSubtitle = null!, AdminStatus = null!, ProgressText = null!;
    private ProgressBar OperationProgress = null!;
    // sidebar & tab
    private ColumnDefinition SidebarColumn = null!;
    private Border Sidebar = null!;
    private TabControl MainTabs = null!;
    private TabItem DashboardTab = null!, ProcessesTab = null!, StartupTab = null!, AppsTab = null!, CleanupTab = null!,
                    SystemTab = null!, TroubleshootTab = null!, ToolsTab = null!, SettingsTab = null!;
    // dashboard
    private TextBlock WelcomeText = null!, WelcomeHint = null!, HealthValue = null!, ProcessCountValue = null!, StartupCountValue = null!, FreeSpaceValue = null!;
    // proses
    private TextBox ProcessSearch = null!;
    private CheckBox ShowSystemProcesses = null!;
    private ListView ProcessList = null!;
    // startup
    private ListView StartupList = null!;
    // apps
    private TextBox AppSearch = null!;
    private ListView AppsList = null!, LeftoverList = null!;
    // cleanup
    private CheckBox IncludeBrowserCache = null!, IncludeDownloads = null!;
    private ListView CleanupList = null!;
    private TextBlock CleanupTotal = null!;
    // sistem
    private TextBox SystemInfoBox = null!;
    // troubleshooting
    private ListView TroubleList = null!;
    private TextBlock TroubleName = null!, TroubleDescription = null!;
    private TextBox TroubleOutput = null!;
    private Button RunTroubleButton = null!;
    // tools
    private ComboBox PowerPlanBox = null!;
    private TextBox DuplicatePathBox = null!, DuplicateOutput = null!;
    private Button ScanDuplicatesButton = null!, CancelDuplicatesButton = null!;
    // pengaturan
    private TextBlock SettingsTitle = null!, LogRetentionText = null!, SettingsPathText = null!;
    private ComboBox LanguageBox = null!, CleanupFrequencyBox = null!;
    private CheckBox TrayBox = null!, HideSystemBox = null!, ConfirmBox = null!, ScheduledCleanupBox = null!;

    private void BindControls()
    {
        SidebarToggleButton = F<Button>(nameof(SidebarToggleButton));
        ElevateButton = F<Button>(nameof(ElevateButton));
        RefreshDashboardButton = F<Button>(nameof(RefreshDashboardButton));
        AppSubtitle = F<TextBlock>(nameof(AppSubtitle));
        AdminStatus = F<TextBlock>(nameof(AdminStatus));
        ProgressText = F<TextBlock>(nameof(ProgressText));
        OperationProgress = F<ProgressBar>(nameof(OperationProgress));
        SidebarColumn = F<ColumnDefinition>(nameof(SidebarColumn));
        Sidebar = F<Border>(nameof(Sidebar));
        MainTabs = F<TabControl>(nameof(MainTabs));
        DashboardTab = F<TabItem>(nameof(DashboardTab));
        ProcessesTab = F<TabItem>(nameof(ProcessesTab));
        StartupTab = F<TabItem>(nameof(StartupTab));
        AppsTab = F<TabItem>(nameof(AppsTab));
        CleanupTab = F<TabItem>(nameof(CleanupTab));
        SystemTab = F<TabItem>(nameof(SystemTab));
        TroubleshootTab = F<TabItem>(nameof(TroubleshootTab));
        ToolsTab = F<TabItem>(nameof(ToolsTab));
        SettingsTab = F<TabItem>(nameof(SettingsTab));
        WelcomeText = F<TextBlock>(nameof(WelcomeText));
        WelcomeHint = F<TextBlock>(nameof(WelcomeHint));
        HealthValue = F<TextBlock>(nameof(HealthValue));
        ProcessCountValue = F<TextBlock>(nameof(ProcessCountValue));
        StartupCountValue = F<TextBlock>(nameof(StartupCountValue));
        FreeSpaceValue = F<TextBlock>(nameof(FreeSpaceValue));
        ProcessSearch = F<TextBox>(nameof(ProcessSearch));
        ShowSystemProcesses = F<CheckBox>(nameof(ShowSystemProcesses));
        ProcessList = F<ListView>(nameof(ProcessList));
        StartupList = F<ListView>(nameof(StartupList));
        AppSearch = F<TextBox>(nameof(AppSearch));
        AppsList = F<ListView>(nameof(AppsList));
        LeftoverList = F<ListView>(nameof(LeftoverList));
        IncludeBrowserCache = F<CheckBox>(nameof(IncludeBrowserCache));
        IncludeDownloads = F<CheckBox>(nameof(IncludeDownloads));
        CleanupList = F<ListView>(nameof(CleanupList));
        CleanupTotal = F<TextBlock>(nameof(CleanupTotal));
        SystemInfoBox = F<TextBox>(nameof(SystemInfoBox));
        TroubleList = F<ListView>(nameof(TroubleList));
        TroubleName = F<TextBlock>(nameof(TroubleName));
        TroubleDescription = F<TextBlock>(nameof(TroubleDescription));
        TroubleOutput = F<TextBox>(nameof(TroubleOutput));
        RunTroubleButton = F<Button>(nameof(RunTroubleButton));
        PowerPlanBox = F<ComboBox>(nameof(PowerPlanBox));
        DuplicatePathBox = F<TextBox>(nameof(DuplicatePathBox));
        DuplicateOutput = F<TextBox>(nameof(DuplicateOutput));
        ScanDuplicatesButton = F<Button>(nameof(ScanDuplicatesButton));
        CancelDuplicatesButton = F<Button>(nameof(CancelDuplicatesButton));
        SettingsTitle = F<TextBlock>(nameof(SettingsTitle));
        LogRetentionText = F<TextBlock>(nameof(LogRetentionText));
        SettingsPathText = F<TextBlock>(nameof(SettingsPathText));
        LanguageBox = F<ComboBox>(nameof(LanguageBox));
        CleanupFrequencyBox = F<ComboBox>(nameof(CleanupFrequencyBox));
        TrayBox = F<CheckBox>(nameof(TrayBox));
        HideSystemBox = F<CheckBox>(nameof(HideSystemBox));
        ConfirmBox = F<CheckBox>(nameof(ConfirmBox));
        ScheduledCleanupBox = F<CheckBox>(nameof(ScheduledCleanupBox));
    }

    private void Click(string name, RoutedEventHandler handler) => F<Button>(name).Click += handler;

    private void BindEvents()
    {
        // header
        SidebarToggleButton.Click += (_, _) => ToggleSidebar();
        ElevateButton.Click += (_, _) => ElevateRestart();
        RefreshDashboardButton.Click += async (_, _) => await RefreshDashboardAsync();

        // navigasi
        Click("NavDashboard", (_, _) => Navigate(DashboardTab));
        Click("NavProcesses", (_, _) => Navigate(ProcessesTab));
        Click("NavStartup", (_, _) => Navigate(StartupTab));
        Click("NavApps", (_, _) => Navigate(AppsTab));
        Click("NavCleanup", (_, _) => Navigate(CleanupTab));
        Click("NavSystem", (_, _) => Navigate(SystemTab));
        Click("NavTroubleshoot", (_, _) => Navigate(TroubleshootTab));
        Click("NavTools", (_, _) => Navigate(ToolsTab));
        Click("NavSettings", (_, _) => Navigate(SettingsTab));
        MainTabs.SelectionChanged += (s, e) => { if (e.Source == MainTabs) UpdateProcessTimer(); };
        IsVisibleChanged += (_, _) => UpdateProcessTimer();

        // dashboard
        Click("OpenCleanupButton", (_, _) => Navigate(CleanupTab));
        Click("OpenSystemButton", async (_, _) => { Navigate(SystemTab); await LoadSystemInfoAsync(); });
        Click("OpenTroubleshootButton", (_, _) => Navigate(TroubleshootTab));
        Click("OpenAppsButton", (_, _) => Navigate(AppsTab));

        // proses
        Click("RefreshProcessesButton", async (_, _) => await RefreshProcessesAsync(showError: true));
        ProcessSearch.TextChanged += (_, _) => FilterProcesses();
        ShowSystemProcesses.Checked += async (_, _) => await RefreshProcessesAsync(showError: false);
        ShowSystemProcesses.Unchecked += async (_, _) => await RefreshProcessesAsync(showError: false);
        Click("KillProcessButton", async (_, _) => await KillSelectedProcessAsync(tree: false));
        Click("KillTreeButton", async (_, _) => await KillSelectedProcessAsync(tree: true));

        // auto-start
        Click("RefreshStartupButton", async (_, _) => await RefreshStartupAsync());
        Click("DisableStartupButton", async (_, _) => await SetStartupEnabledAsync(false));
        Click("EnableStartupButton", async (_, _) => await SetStartupEnabledAsync(true));

        // uninstaller
        Click("RefreshAppsButton", async (_, _) => await RefreshAppsAsync());
        AppSearch.TextChanged += (_, _) => FilterApps();
        Click("UninstallButton", async (_, _) => await UninstallSelectedAsync());
        Click("ScanLeftoverButton", async (_, _) => await ScanLeftoverAsync());
        Click("RemoveLeftoverButton", async (_, _) => await RemoveLeftoverAsync());

        // pembersihan
        Click("ScanCleanupButton", async (_, _) => await ScanCleanupAsync());
        Click("CleanSelectedButton", async (_, _) => await CleanSelectedAsync());
        Click("CleanupDuplicatesButton", async (_, _) => { Navigate(ToolsTab); await FindDuplicatesAsync(); });

        // spesifikasi
        Click("LoadSystemInfoButton", async (_, _) => await LoadSystemInfoAsync());
        Click("ExportSystemInfoButton", async (_, _) => await ExportSystemInfoAsync());

        // troubleshooting
        TroubleList.SelectionChanged += (_, _) => ShowTroubleSelection();
        RunTroubleButton.Click += async (_, _) => await RunTroubleAsync();

        // tools
        Click("ApplyPowerPlanButton", async (_, _) => await ApplyPowerPlanAsync());
        ScanDuplicatesButton.Click += async (_, _) => await FindDuplicatesAsync();
        CancelDuplicatesButton.Click += (_, _) => _duplicateCts?.Cancel();
        Click("BatteryReportButton", async (_, _) => await BatteryReportAsync());
        Click("OpenWindowsSettingsButton", (_, _) => OpenWindowsSettings());

        // pengaturan
        LanguageBox.SelectionChanged += (_, _) => { if (!_loadingSettings && IsLoaded) { OnSettingChanged(); ReadSettingsFromUi(); ApplyLanguage(); } };
        CleanupFrequencyBox.SelectionChanged += (_, _) => OnSettingChanged();
        foreach (var box in new[] { TrayBox, HideSystemBox, ConfirmBox, ScheduledCleanupBox })
        {
            box.Checked += (_, _) => OnSettingChanged();
            box.Unchecked += (_, _) => OnSettingChanged();
        }
        HideSystemBox.Checked += (_, _) => { if (!_loadingSettings) ShowSystemProcesses.IsChecked = false; };
        HideSystemBox.Unchecked += (_, _) => { if (!_loadingSettings) ShowSystemProcesses.IsChecked = true; };
        Click("ClearLogsButton", (_, _) => ClearLogs());
        Click("ManageExclusionsButton", (_, _) => ManageExclusions());
        Click("SaveSettingsButton", async (_, _) => await SaveAndApplySettingsAsync(announce: true));
    }
}
