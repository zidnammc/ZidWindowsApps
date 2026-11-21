# Audit migrasi UI · 21 November 2026

ZidWindowsApps 1.1.0 menggunakan WPF pada .NET 8. Program.Main memuat UI/App.cs, kemudian XamlReader memuat Resources/MainWindow.xaml dan Styles.xaml dari assembly. Handler di UI/MainWindow.* memanggil layanan C# dalam proses yang sama. WinForms menyediakan NotifyIcon. Build Windows memakai dotnet build, distribusi memakai build.ps1, dotnet publish dan NSIS. Versi berasal dari ZidWindowsApps.csproj.

DLL pada dist-selfcontained-win-x64 identik dengan instalasi di Program Files (SHA-256 A00F6FB207E56EBD3EA3F96DD96BC34D55AA8F830CB91D774EC9F80A867C3D21). Folder sumber ini tidak mempunyai riwayat Git. Cadangan lengkap tersimpan di C:\Users\Lenovo\Backup\ZidWindowsApps-pre-new-ui-20261121-070431 (990 file, 387.357.409 byte). Pemilik memilih B1, WPF native.

| Modul | Fungsi yang sudah ada dan batas integrasinya |
|---|---|
| Shell | Admin.IsAdministrator membaca token. Elevasi memakai runas dan argumen --elevated. Pemulihan tab dan sampel CPU seluruh perangkat perlu integrasi. |
| Dashboard | RefreshDashboardAsync menghitung jumlah proses, auto-start, RAM dan drive. Skor numerik, cache terakhir, dan waktu scan perlu definisi. |
| Processes | ProcessService.GetProcesses(includeSystem), IsCritical dan Kill(info, tree). Akses bisa ditolak per PID. Publisher berasal dari metadata file, bukan pemeriksaan tanda tangan digital. |
| Auto-start | StartupService.GetItems dan SetEnabled(item, enabled), termasuk folder startup, registry 32/64 bit dan layanan. HKLM dan perubahan layanan butuh izin. Pertahankan pemilihan banyak item. |
| Uninstaller | InstalledAppsService.GetInstalledApps, UninstallFully, FindLeftovers dan TryRemoveLeftover. Hasil uninstall harus diverifikasi. Penghapusan registry mencadangkan .reg, dapat gagal karena izin. |
| Cleanup | CleanupService.Scan dan Delete menerima token pembatalan. Hasil memisahkan deleted, failed, skipped, freedBytes. RecycleBinService.TrySend memindahkan file. Pengosongan Recycle Bin belum memiliki antarmuka layanan. |
| System Info | SystemInfoService.GetSnapshotAsync menghasilkan SystemSnapshot berisi teks. ExportReportAsync(snapshot, path) terhubung ke dialog simpan. Postur keamanan terstruktur memerlukan pemetaan status dan izin. |
| Troubleshooting | TroubleshootingService.GetItems menyediakan sembilan TroubleItem, metadata perubahan dan kebutuhan admin. Action menghasilkan satu hasil setelah selesai. Keluaran per baris belum tersedia. Cocokkan label SFC dengan perintah lama /scannow. |
| Tools | ApplyPowerPlanAsync, BatteryReportAsync, OpenWindowsSettings, DuplicateService.Find(root, progress, ct). Pertahankan pembatalan dan pilihan folder. Deteksi skema aktif, dua pintasan tambahan dan pemilihan file duplikat untuk dihapus memerlukan penghubung. |
| Settings | AppSettings.Load/Save, App.ConfigureTray, ScheduledCleanupService.Configure, AppPaths dan ActivityLogger.ClearAll. Pengecualian folder, retensi log dan kompatibilitas bahasa lama perlu dipertahankan. |

Pengaturan disimpan atomik di %APPDATA%\ZidWindowsApps\settings.json. Skema lama memuat Language, HideSystemProcesses, ConfirmBeforeAction, EnableTray, ScheduledCleanupEnabled, LogRetentionDays, CleanupFrequency dan ExcludedPaths. Data lokal memakai %LOCALAPPDATA%\ZidWindowsApps\Logs dan RegistryBackups. Task Scheduler menjalankan mode --scheduled-cleanup. Menutup jendela bisa menyembunyikannya ke tray. Jalur pratinjau harus melewati konfigurasi tray, log, scheduler dan elevasi produksi.

Bukti visual dan hasil pengukuran dicatat dalam PROGRESS.md.
