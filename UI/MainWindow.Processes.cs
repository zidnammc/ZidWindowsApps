using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ZidWindowsApps;

public sealed partial class MainWindow
{
    private List<ProcessInfo> _allProcesses = new();
    private bool _refreshingProcesses;

    private async Task RefreshProcessesAsync(bool showError)
    {
        if (_refreshingProcesses) return;           // cegah refresh bertumpuk (satu refresh bisa >3 detik)
        _refreshingProcesses = true;
        try
        {
            bool includeSystem = ShowSystemProcesses.IsChecked == true;
            _allProcesses = await Task.Run(() => ProcessService.GetProcesses(includeSystem));
            FilterProcesses();
            ProgressText.Text = $"{_allProcesses.Count} proses.";
        }
        catch (Exception ex)
        {
            if (showError) ShowError("Membaca daftar proses", ex);
        }
        finally { _refreshingProcesses = false; }
    }

    private void FilterProcesses()
    {
        var text = ProcessSearch.Text.Trim();
        IEnumerable<ProcessInfo> query = _allProcesses;
        if (text.Length > 0)
            query = query.Where(p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                     p.Publisher.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                     p.Path.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                     p.Id.ToString() == text);
        var list = query.ToList();

        // Pertahankan pilihan dan posisi scroll supaya auto-refresh tidak "melepas" proses yang sedang dipilih.
        int? selectedId = (ProcessList.SelectedItem as ProcessInfo)?.Id;
        var scroll = FindVisualChild<ScrollViewer>(ProcessList);
        double offset = scroll?.VerticalOffset ?? 0;

        ProcessList.ItemsSource = list;

        if (selectedId.HasValue) ProcessList.SelectedItem = list.FirstOrDefault(p => p.Id == selectedId.Value);
        if (offset > 0)
        {
            ProcessList.UpdateLayout();
            (FindVisualChild<ScrollViewer>(ProcessList) ?? scroll)?.ScrollToVerticalOffset(offset);
        }
    }

    private async Task KillSelectedProcessAsync(bool tree)
    {
        if (ProcessList.SelectedItem is not ProcessInfo process)
        {
            Info("Pilih proses terlebih dahulu.");
            return;
        }
        if (ProcessService.IsCritical(process))
        {
            Info($"'{process.Name}' adalah proses inti Windows. Menghentikannya dapat membuat Windows crash, jadi tidak diizinkan.");
            return;
        }

        var risky = process.IsSystem || process.IsProtected;
        var message = tree
            ? $"Hentikan '{process.Name}' (PID {process.Id}) beserta SEMUA proses turunannya?"
            : $"Hentikan '{process.Name}' (PID {process.Id})? Data yang belum disimpan di proses itu bisa hilang.";
        if (risky) message += "\n\nPERINGATAN: ini proses sistem/dilindungi. Menghentikannya bisa membuat Windows tidak stabil.";
        if (!ConfirmAction(message, force: risky)) return;

        try
        {
            SetBusy($"Menghentikan {process.Name}...", true);
            await Task.Run(() => ProcessService.Kill(process, tree));    // tidak lagi membekukan UI
            App.Logger.Write($"Proses dihentikan: {process.Name} (PID {process.Id}){(tree ? " + turunan" : "")}");
        }
        catch (Exception ex) { ShowError($"Menghentikan {process.Name}", ex); }
        finally { SetBusy("Siap", false); }

        await RefreshProcessesAsync(showError: false);
    }
}
