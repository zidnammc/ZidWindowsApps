using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ZidWindowsApps.Fluent;
internal sealed partial class LiveWindow
{
    protected override string NavLabel(string label) => App.Settings.Language == "en" ? label : label switch
    {
        "Processes" => "Proses",
        "Cleanup" => "Pembersihan",
        "System Info" => "Info Sistem",
        "Settings" => "Pengaturan",
        _ => label
    };
    private AppSettings? draft;
    private static AppSettings CopySettings(AppSettings s) => new()
    {
        Language = s.Language,
        EnableTray = s.EnableTray,
        HideSystemProcesses = s.HideSystemProcesses,
        ConfirmBeforeAction = s.ConfirmBeforeAction,
        ScheduledCleanupEnabled = s.ScheduledCleanupEnabled,
        CleanupFrequency = s.CleanupFrequency,
        LogRetentionDays = s.LogRetentionDays,
        ExcludedPaths = new List<string>(s.ExcludedPaths),
        DarkTheme = s.DarkTheme,
        SidebarCollapsed = s.SidebarCollapsed
    };
    private UIElement SettingsPage()
    {
        var s = draft ??= CopySettings(App.Settings);
        bool Dirty()
        {
            var a = App.Settings;
            return s.Language != a.Language || s.EnableTray != a.EnableTray || s.HideSystemProcesses != a.HideSystemProcesses || s.ConfirmBeforeAction != a.ConfirmBeforeAction || s.ScheduledCleanupEnabled != a.ScheduledCleanupEnabled || s.CleanupFrequency != a.CleanupFrequency || !s.ExcludedPaths.SequenceEqual(a.ExcludedPaths);
        }

        var dirty = Label("", 12, false, "Muted");
        var save = ActionButton("Simpan Pengaturan", () => _ = Work("Menyimpan pengaturan", async (_, _) =>
        {
            var previous = App.Settings;
            s.DarkTheme = Dark;
            s.SidebarCollapsed = Collapsed;
            bool scheduleChanged = s.ScheduledCleanupEnabled != previous.ScheduledCleanupEnabled || (s.ScheduledCleanupEnabled && s.CleanupFrequency != previous.CleanupFrequency);
            if (scheduleChanged)
            {
                var change = await Task.Run(() => ScheduledCleanupService.Configure(s.ScheduledCleanupEnabled, s.CleanupFrequency));
                if (!change.Ok)
                    throw new InvalidOperationException(change.Message);
            }

            if (!s.Save(out var err))
            {
                if (scheduleChanged)
                {
                    var rollback = await Task.Run(() => ScheduledCleanupService.Configure(previous.ScheduledCleanupEnabled, previous.CleanupFrequency));
                    if (!rollback.Ok)
                        err += " · Gagal memulihkan jadwal: " + rollback.Message;
                }

                throw new InvalidOperationException(err);
            }

            App.UseSettings(s);
            draft = null;
            showSystem = !s.HideSystemProcesses;
            App.ConfigureTray();
            return "Pengaturan tersimpan.";
        }), "primary");
        void Changed()
        {
            save.IsEnabled = Dirty();
            dirty.Text = Dirty() ? "Perubahan belum disimpan" : "";
        }

        var general = Card(Stack(Label("Umum & Tampilan", 16, true), Row(Label("Bahasa navigasi"), Segments(new[] { "Bahasa Indonesia", "English" }, s.Language == "en" ? "English" : "Bahasa Indonesia", v =>
        {
            s.Language = v == "English" ? "en" : "id";
            Changed();
        })), Row(Label("Tema antarmuka"), Segments(new[] { "Terang", "Gelap" }, Dark ? "Gelap" : "Terang", v =>
        {
            if (Dark != (v == "Gelap"))
                ToggleTheme();
        }))));
        var behavior = Card(Stack(Label("Perilaku & Keamanan", 16, true), Check("Tetap berjalan di system tray saat window ditutup", s.EnableTray, v =>
        {
            s.EnableTray = v;
            Changed();
        }), Check("Sembunyikan proses sistem secara default", s.HideSystemProcesses, v =>
        {
            s.HideSystemProcesses = v;
            Changed();
        }), Check("Minta konfirmasi sebelum tindakan", s.ConfirmBeforeAction, v =>
        {
            s.ConfirmBeforeAction = v;
            Changed();
        }), Label("Tindakan berisiko selalu meminta konfirmasi.", 12, false, "Muted")));
        var frequency = Segments(new[] { "Harian", "Mingguan", "Bulanan" }, s.CleanupFrequency, v =>
        {
            s.CleanupFrequency = v;
            Changed();
        });
        frequency.IsEnabled = s.ScheduledCleanupEnabled;
        var schedule = Stack(Label("Pembersihan Terjadwal", 16, true), Check("Aktifkan pembersihan terjadwal (03:00)", s.ScheduledCleanupEnabled, v =>
        {
            s.ScheduledCleanupEnabled = v;
            frequency.IsEnabled = v;
            Changed();
        }), frequency);
        schedule.Children.Add(ActionButton("Tambah folder pengecualian", () =>
        {
            if (PickFolder()is string dir && !s.ExcludedPaths.Contains(dir, StringComparer.OrdinalIgnoreCase))
            {
                s.ExcludedPaths.Add(dir);
                Render();
            }
        }));
        foreach (var path in s.ExcludedPaths.ToArray())
            schedule.Children.Add(Row(Label(path, 12), ActionButton("Keluarkan", () =>
            {
                s.ExcludedPaths.Remove(path);
                Render();
            })));
        UIElement Location(string title, string path) => Stack(Label(title, 12, true, "Muted"), Row(Label(path, 12), ActionButton("Salin", () => Copy(path))));
        var locations = Card(Stack(Label("Log & Lokasi Data", 16, true), Location("Pengaturan", AppPaths.SettingsFile), Location("Log aktivitas", AppPaths.LogsDirectory), Location("Cadangan registry", AppPaths.RegistryBackupDirectory), Label("Retensi log · " + s.LogRetentionDays + " hari", 12, false, "Muted"), ActionButton("Hapus semua log aktivitas", () => ConfirmWork("Hapus semua log aktivitas?", AppPaths.LogsDirectory, "Hapus", async (_, _) => await Task.Run(() => App.Logger.ClearAll() ? "Log aktivitas dihapus." : "Sebagian log gagal dihapus.")), "danger")));
        Changed();
        var actions = Row(dirty, save);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        var page = Stack(general, behavior, Card(schedule), locations, actions);
        page.MaxWidth = 900;
        return Scroll(page);
    }
}
