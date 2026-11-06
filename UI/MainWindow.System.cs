using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ZidWindowsApps;

// Tab Spesifikasi dan Troubleshooting.
public sealed partial class MainWindow
{
    private SystemSnapshot? _snapshot;

    private static string RenderSnapshot(SystemSnapshot snapshot)
    {
        var sb = new StringBuilder();
        foreach (var p in typeof(SystemSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            sb.AppendLine("[" + p.Name + "]");
            sb.AppendLine(Convert.ToString(p.GetValue(snapshot)));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task LoadSystemInfoAsync()
    {
        try
        {
            SetBusy("Membaca informasi perangkat (bisa 10-20 detik)...", true);
            SystemInfoBox.Text = "Membaca informasi perangkat...";
            _snapshot = await SystemInfoService.GetSnapshotAsync();
            SystemInfoBox.Text = RenderSnapshot(_snapshot);
            SetBusy("Informasi perangkat selesai dibaca.", false);
        }
        catch (Exception ex)
        {
            SystemInfoBox.Text = "Gagal membaca informasi: " + ex.Message;
            SetBusy("Siap", false);
            App.Logger.Write("Info sistem gagal: " + ex.Message);
        }
    }

    private async Task ExportSystemInfoAsync()
    {
        if (_snapshot == null) await LoadSystemInfoAsync();
        if (_snapshot == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Simpan laporan sistem",
            Filter = "File teks (*.txt)|*.txt",
            FileName = $"ZidWindowsApps-sistem-{DateTime.Now:yyyyMMdd-HHmm}.txt",
            DefaultExt = ".txt",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            await SystemInfoService.ExportReportAsync(_snapshot, dialog.FileName);
            Info("Laporan disimpan:\n" + dialog.FileName);
        }
        catch (Exception ex) { ShowError("Menyimpan laporan", ex); }
    }

    // ------------------------------------------------------------------ troubleshooting

    private void ShowTroubleSelection()
    {
        if (TroubleList.SelectedItem is not TroubleItem item) return;
        TroubleName.Text = item.Name;
        TroubleDescription.Text = item.Description + (item.RequiresAdmin && !Admin.IsAdministrator ? "\n(Butuh Administrator - saat ini tidak berjalan sebagai Administrator.)" : "");
        TroubleOutput.Text = "";
    }

    private async Task RunTroubleAsync()
    {
        if (TroubleList.SelectedItem is not TroubleItem item) { Info("Pilih pemeriksaan terlebih dahulu."); return; }

        var message = $"Jalankan '{item.Name}'?";
        if (item.ChangesSystem) message += "\n\nTindakan ini mengubah/menjalankan perintah sistem.";
        if (!ConfirmAction(message)) return;

        RunTroubleButton.IsEnabled = false;
        TroubleOutput.Text = "Menjalankan... (beberapa pemeriksaan butuh beberapa menit)";
        SetBusy($"Menjalankan {item.Name}...", true);
        try
        {
            var output = await item.Action();
            TroubleOutput.Text = output;
            App.Logger.Write($"Troubleshooting dijalankan: {item.Name}");
        }
        catch (Exception ex) { TroubleOutput.Text = "Gagal: " + ex.Message; }
        finally
        {
            RunTroubleButton.IsEnabled = true;
            SetBusy("Siap", false);
        }
    }
}
