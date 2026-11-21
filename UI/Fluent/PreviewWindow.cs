using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZidWindowsApps.Fluent;
internal sealed partial class PreviewWindow : FluentWindow
{
    private readonly PreviewData data = PreviewData.Load();
    private readonly PreviewPreferences prefs = PreviewPreferences.Load();
    private bool admin;
    protected override bool IsPreview => true;
    protected override bool Dark { get => prefs.Dark; set => prefs.Dark=value; }
    protected override bool Collapsed { get => prefs.Collapsed; set => prefs.Collapsed=value; }
    protected override bool AdminMode => admin;
    internal PreviewWindow()
    {
        InitializeShell();
        Title="ZidWindowsApps · Pratinjau Fluent";
        Get<TextBlock>("Status").Text="Pratinjau · Data contoh";
        Get<TextBlock>("Telemetry").Text="CPU 24%    RAM 88%    C: 99,67 GB bebas";
        showSystem = !prefs.HideSystem;
        Closed += (_,_)=>diagnosticRun?.Cancel();
        Navigate("dashboard");
    }
    protected override void LeavingPage() => diagnosticRun?.Cancel();
    protected override void RefreshPage() { Navigate(current); Notify("Tampilan data contoh disegarkan."); }
    protected override void RequestAdmin() => RequestSimulatedAdmin();
    internal static int RunPreview()
    {
        var application = new Application();
        return application.Run(new PreviewWindow());
    }

    protected override void SavePreferences()
    {
        try
        {
            var saved = PreviewPreferences.Load();
            saved.Dark = prefs.Dark;
            saved.Collapsed = prefs.Collapsed;
            saved.Save();
        }
        catch (Exception ex)when (ex is IOException or UnauthorizedAccessException)
        {
            Notify("Preferensi gagal disimpan: " + ex.Message);
        }
    }

    private void RequestSimulatedAdmin() => Ask("Simulasikan hak akses?", "Pilihan ini mengubah hak akses pada data contoh. Setelah berganti mode, pilih kembali tindakan yang ingin dijalankan.", "Ubah mode", () =>
    {
        admin = !admin;
        Navigate(current);
    });
    protected override UIElement Page(string key) => key switch
        {
            "processes" => Processes(),
            "autostart" => Startup(),
            "uninstaller" => Apps(),
            "cleanup" => Cleanup(),
            "sysinfo" => SystemInfo(),
            "troubleshooting" => Diagnostics(),
            "tools" => Tools(),
            "settings" => Settings(),
            _ => Dashboard()};
    protected override string? Badge(string key) => key switch
                    {
                        "processes" => data.Processes.Count.ToString(),
                        "autostart" => data.Startup.Count(x => x.Enabled).ToString(),
                        "cleanup" => PreviewData.Size(data.Cleanup.Sum(x => x.SizeMb)),
                        "uninstaller" => data.Apps.Count.ToString(),
                        _ => null
                    };
}
