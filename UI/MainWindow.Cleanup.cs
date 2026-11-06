using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ZidWindowsApps;

// Tab Pembersihan dan Tools (duplikat, power plan, laporan baterai).
public sealed partial class MainWindow
{
    private List<CleanupItem> _cleanupItems = new();
    private CancellationTokenSource? _duplicateCts;

    // ------------------------------------------------------------------ pembersihan

    private async Task ScanCleanupAsync()
    {
        try
        {
            SetBusy("Memindai file yang aman dibersihkan...", true);
            bool browser = IncludeBrowserCache.IsChecked == true, downloads = IncludeDownloads.IsChecked == true;
            var items = await Task.Run(() => new CleanupService(App.Settings).Scan(browser, downloads));

            foreach (var old in _cleanupItems) old.PropertyChanged -= OnCleanupItemChanged;
            _cleanupItems = items;
            foreach (var item in _cleanupItems) item.PropertyChanged += OnCleanupItemChanged;

            CleanupList.ItemsSource = _cleanupItems;
            UpdateCleanupTotal();
            SetBusy(items.Count == 0 ? "Tidak ada yang perlu dibersihkan." : $"Ditemukan {items.Count} item.", false);
        }
        catch (Exception ex) { SetBusy("Siap", false); ShowError("Memindai pembersihan", ex); }
    }

    private void OnCleanupItemChanged(object? sender, PropertyChangedEventArgs e) => UpdateCleanupTotal();

    private void UpdateCleanupTotal()
    {
        var selected = _cleanupItems.Where(i => i.Selected).ToList();
        CleanupTotal.Text = $"Terpilih: {Format.Bytes(selected.Sum(i => i.Size))} dari {Format.Bytes(_cleanupItems.Sum(i => i.Size))}";
    }

    private async Task CleanSelectedAsync()
    {
        // Hanya item yang DICENTANG (dulu semua hasil scan ikut terhapus karena tidak ada checkbox).
        var selected = _cleanupItems.Where(i => i.Selected).ToList();
        if (selected.Count == 0) { Info("Centang kategori yang ingin dibersihkan terlebih dahulu."); return; }

        var size = selected.Sum(i => i.Size);
        var files = selected.Sum(i => i.FileCount);
        var message = $"Pindahkan {files:N0} file ({Format.Bytes(size)}) dari {selected.Count} item ke Recycle Bin?";
        if (selected.Any(i => i.Category.StartsWith("Downloads", StringComparison.Ordinal)))
            message += "\n\nTermasuk file dari folder Downloads. Pastikan Anda tidak membutuhkannya.";
        if (selected.Any(i => i.Category.Contains("Browser", StringComparison.OrdinalIgnoreCase) || i.Category.StartsWith("Cache Microsoft Edge") || i.Category.StartsWith("Cache Google Chrome")))
            message += "\n\nTutup Edge/Chrome terlebih dahulu agar cache bisa dibersihkan.";
        if (!ConfirmAction(message)) return;

        try
        {
            SetBusy("Membersihkan...", true);
            var progress = new Progress<string>(t => ProgressText.Text = t);
            var result = await Task.Run(() => new CleanupService(App.Settings).Delete(selected, scheduled: false, progress));
            var summary = $"{result.Deleted:N0} file dipindahkan ke Recycle Bin ({Format.Bytes(result.FreedBytes)}), {result.Failed:N0} gagal (biasanya sedang dipakai), {result.Skipped:N0} dilewati.";
            App.Logger.Write("Pembersihan: " + summary);
            SetBusy(summary, false);
            Info(summary + "\n\nRuang disk baru benar-benar bebas setelah Recycle Bin dikosongkan.");
        }
        catch (Exception ex) { SetBusy("Siap", false); ShowError("Pembersihan", ex); }

        await ScanCleanupAsync();
    }

    // ------------------------------------------------------------------ file duplikat

    private async Task FindDuplicatesAsync()
    {
        if (_duplicateCts != null) return;
        var root = Environment.ExpandEnvironmentVariables(DuplicatePathBox.Text.Trim().Trim('"'));
        if (!Directory.Exists(root)) { Info("Folder tidak ditemukan."); return; }

        var cts = _duplicateCts = new CancellationTokenSource();
        ScanDuplicatesButton.IsEnabled = false;
        CancelDuplicatesButton.IsEnabled = true;
        DuplicateOutput.Text = "Memulai...";
        var progress = new Progress<string>(t => DuplicateOutput.Text = t);
        try
        {
            var groups = await Task.Run(() => DuplicateService.Find(root, progress, cts.Token));
            if (groups.Count == 0) { DuplicateOutput.Text = "Tidak ditemukan file duplikat."; return; }

            var sb = new StringBuilder();
            long wasted = groups.Sum(g => g.Size * (g.Files.Count - 1));
            sb.AppendLine($"{groups.Count} kelompok duplikat, potensi penghematan {Format.Bytes(wasted)}.");
            sb.AppendLine();
            foreach (var g in groups.Take(100))
            {
                sb.AppendLine($"{Format.Bytes(g.Size)} x {g.Files.Count} file:");
                foreach (var f in g.Files) sb.AppendLine("  " + f);
                sb.AppendLine();
            }
            if (groups.Count > 100) sb.AppendLine($"... dan {groups.Count - 100} kelompok lain (ditampilkan 100 terbesar).");
            DuplicateOutput.Text = sb.ToString();
        }
        catch (OperationCanceledException) { DuplicateOutput.Text = "Pemindaian dibatalkan."; }
        catch (Exception ex) { DuplicateOutput.Text = "Gagal: " + ex.Message; }
        finally
        {
            _duplicateCts = null;
            cts.Dispose();
            ScanDuplicatesButton.IsEnabled = true;
            CancelDuplicatesButton.IsEnabled = false;
        }
    }

    // ------------------------------------------------------------------ tools

    private static readonly HashSet<string> PowerSchemes = new() { "SCHEME_BALANCED", "SCHEME_MIN", "SCHEME_MAX" };

    private async Task ApplyPowerPlanAsync()
    {
        if (PowerPlanBox.SelectedItem is not ComboBoxItem item) return;
        var scheme = item.Tag?.ToString() ?? "";
        if (!PowerSchemes.Contains(scheme)) return;
        if (!ConfirmAction($"Terapkan rencana daya '{item.Content}'?")) return;

        try
        {
            SetBusy("Menerapkan rencana daya...", true);
            var r = await PowerShellService.RunAsync("powercfg /setactive " + scheme, 30);
            SetBusy("Siap", false);
            Info(r.Success ? "Rencana daya diterapkan." : "Gagal menerapkan rencana daya:\n" + (string.IsNullOrWhiteSpace(r.Combined) ? $"kode {r.ExitCode}" : r.Combined));
            if (r.Success) App.Logger.Write("Power plan: " + item.Content);
        }
        catch (Exception ex) { SetBusy("Siap", false); ShowError("Mengubah rencana daya", ex); }
    }

    private async Task BatteryReportAsync()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var path = Path.Combine(AppPaths.DataDirectory, "battery-report.html");
            SetBusy("Membuat laporan baterai...", true);
            var r = await PowerShellService.RunAsync($"powercfg /batteryreport /output {PowerShellService.Quote(path)}", 60);
            SetBusy("Siap", false);

            if (r.Success && File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                Info("Laporan baterai tidak dapat dibuat (perangkat tanpa baterai?).\n\n" + r.Combined);
        }
        catch (Exception ex) { SetBusy("Siap", false); ShowError("Membuat laporan baterai", ex); }
    }

    private void OpenWindowsSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("Membuka pengaturan Windows", ex); }
    }
}
