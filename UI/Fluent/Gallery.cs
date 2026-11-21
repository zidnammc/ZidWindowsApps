using System;
using System.Windows;
using System.Windows.Controls;

namespace ZidWindowsApps.Fluent;
internal sealed partial class PreviewWindow
{
    protected override void ShowGallery()
    {
        var close = ActionButton("Tutup", CloseOverlay);
        var state = new ContentControl();
        void Display(string mode)
        {
            state.Content = mode switch
            {
                "Memuat" => Stack(Label("Memuat data contoh…"), new ProgressBar { Height = 5, IsIndeterminate = true }),
                "Kosong" => Label("Belum ada data untuk ditampilkan.", 14, false, "Muted"),
                "Gagal" => Stack(Label("Data contoh gagal dimuat."), ActionButton("Coba lagi", () => Display("Normal"))),
                _ => Stack(Label("Data siap", 15, true), Check("Tampilkan proses sistem", showSystem, v => showSystem = v), Unavailable("Tindakan sistem"))
            };
        }

        Display("Normal");
        OpenOverlay(Stack(Label("Galeri komponen WPF", 18, true), Segments(new[] { "Normal", "Memuat", "Kosong", "Gagal" }, "Normal", Display), Card(state), Row(ActionButton("Dialog", () =>
        {
            CloseOverlay();
            Ask("Konfirmasi tindakan contoh?", "Perubahan hanya berlaku di galeri.", "Lanjutkan", () => Notify("Tindakan contoh dikonfirmasi."));
        }), ActionButton("Toast", () =>
        {
            CloseOverlay();
            Notify("Notifikasi contoh.");
        }), ActionButton("Palet perintah", () =>
        {
            CloseOverlay();
            ShowPalette();
        })), close), close);
    }
}
