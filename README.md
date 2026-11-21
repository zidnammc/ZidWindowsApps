# ZidWindowsApps 1.2.0

Pusat perawatan Windows (WPF, .NET 8): proses, auto-start, uninstaller + leftover, pembersihan, spesifikasi,
troubleshooting, tools. Detail perbaikan: lihat LAPORAN-BUG-DAN-PERBAIKAN.md.

Tampilan Fluent WPF membuka sembilan modul dengan layanan Windows asli. Gunakan Ctrl+K untuk mencari modul. Tema dan ukuran sidebar tersimpan di pengaturan lokal. Untuk kembali ke tampilan sebelumnya, jalankan `ZidWindowsApps.exe --legacy-ui` setelah keluar dari aplikasi melalui tray.

## Build di Windows
Prasyarat: .NET SDK 8+, NSIS.

| Perintah | Hasil |
|---|---|
| `./build.ps1` | `ZidWindowsApps-Setup.exe` (±0,4 MB). Butuh .NET Desktop Runtime 8+ di PC tujuan. |
| `./build.ps1 -SelfContained` | `ZidWindowsApps-Setup-SelfContained-win-x64.exe`. Runtime ikut dibundel, tanpa prasyarat. |
| `./build.ps1 -SelfContained -Rid win-arm64` | Versi self-contained untuk Windows ARM64. |

Build self-contained pertama butuh internet (runtime pack dari NuGet). Hanya aplikasi: `dotnet publish -c Release -o dist`.

## Struktur
- `Infrastructure/` path, pengaturan, log, PathGuard (pagar penghapusan), hak admin
- `Models/` kelas data
- `Services/` logika (tanpa UI): PowerShell, Recycle Bin, pembersihan, duplikat, proses, aplikasi, auto-start, jadwal, info sistem
- `UI/` App, MainWindow (dipecah per tab), InputDialog
- `Resources/` MainWindow.xaml dan Styles.xaml (dimuat saat runtime, tertanam di DLL)
- `launcher/` launcher native kecil (hanya untuk build Linux/CI); di Windows dipakai apphost bawaan SDK
- `installer/` skrip NSIS (Modern UI 2)

Aplikasi butuh .NET Desktop Runtime 8 atau lebih baru. Opsi `--scheduled-cleanup` dipakai Task Scheduler.
