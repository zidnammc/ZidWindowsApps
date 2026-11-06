using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ZidWindowsApps;

// Tab Auto-start dan Uninstaller (termasuk leftover).
public sealed partial class MainWindow
{
    private List<StartupItem> _allStartup = new();
    private List<InstalledApp> _allApps = new();
    private readonly ObservableCollection<LeftoverItem> _leftovers = new();

    // ------------------------------------------------------------------ auto-start

    private async Task RefreshStartupAsync()
    {
        try
        {
            SetBusy("Membaca item auto-start...", true);
            _allStartup = await Task.Run(() => StartupService.GetItems());
            StartupList.ItemsSource = _allStartup;
            SetBusy($"{_allStartup.Count} item auto-start.", false);
        }
        catch (Exception ex) { SetBusy("Gagal membaca auto-start.", false); ShowError("Membaca auto-start", ex); }
    }

    private async Task SetStartupEnabledAsync(bool enable)
    {
        var selected = StartupList.SelectedItems.Cast<StartupItem>().Where(i => i.IsEnabled != enable).ToList();
        if (selected.Count == 0)
        {
            Info(enable ? "Pilih item yang sedang nonaktif." : "Pilih item yang sedang aktif.");
            return;
        }

        bool hasService = selected.Any(i => i.Kind == StartupKind.WindowsService);
        var verb = enable ? "Aktifkan kembali" : "Nonaktifkan";
        var detail = hasService ? "\n\nTermasuk layanan Windows: perubahan ini memengaruhi layanan, bukan hanya auto-start." : "\n\nTidak ada file atau entri yang dihapus; Anda bisa membaliknya kapan saja.";
        if (!ConfirmAction($"{verb} {selected.Count} item auto-start?{detail}", force: hasService)) return;

        var errors = new List<string>();
        SetBusy($"{verb}...", true);
        await Task.Run(() =>
        {
            foreach (var item in selected)
            {
                try { StartupService.SetEnabled(item, enable); App.Logger.Write($"Auto-start {(enable ? "diaktifkan" : "dinonaktifkan")}: {item.Name} ({item.Location})"); }
                catch (Exception ex) { errors.Add($"{item.Name}: {ex.Message}"); }
            }
        });
        SetBusy("Siap", false);

        StartupList.Items.Refresh();
        if (errors.Count > 0) Info($"{errors.Count} item gagal diubah:\n\n" + string.Join("\n", errors.Take(8)));
    }

    // ------------------------------------------------------------------ aplikasi terpasang

    private async Task RefreshAppsAsync()
    {
        try
        {
            SetBusy("Membaca aplikasi terpasang...", true);
            _allApps = await Task.Run(() => InstalledAppsService.GetInstalledApps());
            FilterApps();
            SetBusy($"{_allApps.Count} aplikasi.", false);
        }
        catch (Exception ex) { SetBusy("Gagal membaca aplikasi.", false); ShowError("Membaca aplikasi terpasang", ex); }
    }

    private void FilterApps()
    {
        var text = AppSearch.Text.Trim();
        AppsList.ItemsSource = text.Length == 0
            ? _allApps
            : _allApps.Where(a => a.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                  a.Publisher.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static bool IsThisApp(InstalledApp app)
    {
        if (app.Name.Contains("ZidWindowsApps", StringComparison.OrdinalIgnoreCase)) return true;
        return !string.IsNullOrWhiteSpace(app.InstallLocation) && PathGuard.IsUnder(AppContext.BaseDirectory, app.InstallLocation) && !PathGuard.IsProtected(app.InstallLocation);
    }

    private async Task UninstallSelectedAsync()
    {
        if (AppsList.SelectedItem is not InstalledApp app) { Info("Pilih aplikasi terlebih dahulu."); return; }

        if (IsThisApp(app))
        {
            Info("Ini adalah aplikasi yang sedang berjalan. Hapus melalui Pengaturan Windows > Aplikasi, atau lewat Uninstall ZidWindowsApps di Start Menu.");
            return;
        }
        if (!ConfirmAction($"Uninstall '{app.Name}'?\n\nProses aplikasi dari folder instalasinya akan ditutup lebih dulu, lalu uninstaller resmi dijalankan.", force: true)) return;

        UninstallOutcome outcome;
        try
        {
            SetBusy($"Menjalankan uninstall {app.Name}...", true);
            outcome = await Task.Run(() => InstalledAppsService.UninstallFully(app, waitSeconds: 10));
        }
        catch (Exception ex)
        {
            SetBusy("Siap", false);
            ShowError("Uninstall", ex);
            return;
        }
        SetBusy("Siap", false);
        App.Logger.Write($"Uninstall {app.Name}: {outcome.Message}");

        if (!outcome.Removed)
        {
            // Jangan memindai leftover: aplikasi masih terpasang, folder instalasinya bukan "sisa".
            Info(outcome.Message + "\n\nPencarian leftover dilewati karena aplikasi belum benar-benar terhapus.");
            await RefreshAppsAsync();
            return;
        }

        await RefreshAppsAsync();
        await ScanLeftoverForAsync(app, afterUninstall: true);
    }

    private async Task ScanLeftoverAsync()
    {
        if (AppsList.SelectedItem is not InstalledApp app) { Info("Pilih aplikasi terlebih dahulu."); return; }
        if (!app.IsStoreApp && InstalledAppsService.IsStillInstalled(app))
        {
            Info("Aplikasi ini masih terpasang. Jalankan uninstall dulu; leftover hanya dicari setelah aplikasi benar-benar terhapus.");
            return;
        }
        await ScanLeftoverForAsync(app, afterUninstall: false);
    }

    private async Task ScanLeftoverForAsync(InstalledApp app, bool afterUninstall)
    {
        try
        {
            SetBusy("Mencari sisa file dan registry...", true);
            var excluded = App.Settings.ExcludedPaths.ToList();
            var found = await Task.Run(() => InstalledAppsService.FindLeftovers(app, excluded));
            _leftovers.Clear();
            foreach (var item in found) _leftovers.Add(item);
            LeftoverList.ItemsSource = _leftovers;
            SetBusy($"{found.Count} kandidat leftover.", false);

            var prefix = afterUninstall ? $"'{app.Name}' sudah terhapus. " : "";
            Info(found.Count == 0
                ? prefix + "Tidak ditemukan sisa yang jelas terkait aplikasi ini."
                : prefix + $"Ditemukan {found.Count} kandidat leftover. Periksa dan pilih hanya yang benar-benar milik aplikasi ini.");
        }
        catch (Exception ex) { SetBusy("Siap", false); ShowError("Mencari leftover", ex); }
    }

    private async Task RemoveLeftoverAsync()
    {
        var selected = LeftoverList.SelectedItems.Cast<LeftoverItem>().ToList();
        if (selected.Count == 0) { Info("Pilih item leftover terlebih dahulu."); return; }

        int regCount = selected.Count(i => i.Kind == LeftoverKind.Registry);
        var message = $"Hapus {selected.Count} item leftover?\n\n• File/folder dipindahkan ke Recycle Bin.";
        if (regCount > 0) message += $"\n• {regCount} key registry dicadangkan ke file .reg di:\n  {AppPaths.RegistryBackupDirectory}\n  lalu DIHAPUS dari registry.";
        if (!ConfirmAction(message, force: true)) return;

        var failures = new List<string>();
        var removed = new List<LeftoverItem>();
        SetBusy("Menghapus leftover...", true);
        await Task.Run(() =>
        {
            foreach (var item in selected)
            {
                if (InstalledAppsService.TryRemoveLeftover(item, out var error)) removed.Add(item);
                else failures.Add($"{item.Display}: {error}");
            }
        });
        SetBusy("Siap", false);

        foreach (var item in removed) _leftovers.Remove(item);
        App.Logger.Write($"Leftover: {removed.Count} dihapus, {failures.Count} gagal.");
        Info($"{removed.Count} item dihapus." + (failures.Count > 0 ? $"\n\n{failures.Count} gagal:\n" + string.Join("\n", failures.Take(6)) : ""));
    }
}
