# Laporan: ZidWindowsApps — analisis, bug, dan perbaikan

Sumber analisis: `ZidWindowsApps-Setup.exe` dibongkar (bootstrapper .NET 6 + payload zip), `ZidWindowsApps.dll`
didekompilasi dengan ILSpy (27 tipe, ~3.000 baris C# + XAML), lalu ditulis ulang menjadi proyek bersih.

## 1. Paket installer asli

| Temuan | Dampak | Perbaikan |
|---|---|---|
| Payload aplikasi dibuat untuk **win-arm64**, bootstrapper x64 | Di PC Intel/AMD aplikasi hampir pasti tidak jalan | Build AnyCPU (x64 dan ARM64) |
| Runtime .NET dibundel 2x (bootstrapper + payload), total 200 MB | Download/instal berat | Framework-dependent: installer ±400 KB |
| Entri zip memakai backslash | Tidak standar | Tidak dipakai lagi |
| Bootstrapper custom | Tidak punya Add/Remove Programs yang rapi | Wizard NSIS (Modern UI 2) + uninstaller + cek .NET |

## 2. Bug yang berisiko kehilangan data

1. **Pembersihan menghapus semua hasil scan.** Daftar tidak punya checkbox; properti `Selected` selalu true, jadi
   "Kirim terpilih" mengirim semuanya (termasuk Downloads >30 hari bila dicentang). *Sekarang:* checkbox per kategori,
   Downloads per file dan tidak tercentang otomatis, total menghitung item terpilih.
2. **`FOF_NOCONFIRMATION` pada SHFileOperation** membuat file yang terlalu besar untuk Recycle Bin dihapus permanen
   tanpa tanya. *Sekarang:* `FOF_WANTNUKEWARNING`, `FOF_NOERRORUI`, hasil `fAnyOperationsAborted` diperiksa, mode
   terjadwal melewati file >100 MB.
3. **Leftover bisa menyarankan folder terlalu luas** (Program Files, drive root, Users) karena `InstallLocation`
   dipercaya begitu saja; key registry dihapus permanen sementara UI bilang "ke Recycle Bin". *Sekarang:* `PathGuard`
   menolak folder wadah/Windows; registry dicadangkan ke `.reg` (jika cadangan gagal, tidak dihapus); hanya
   `HKCU\Software` dan `HKLM\SOFTWARE`; kata kunci minimal 4 karakter dan nama uninstaller generik diabaikan.
4. **Uninstall tidak memeriksa hasil.** Bila wizard uninstall dibatalkan, folder app yang masih terpasang tetap
   diusulkan sebagai leftover. *Sekarang:* menunggu entri registry benar-benar hilang; leftover hanya dipindai
   setelah app terhapus; "Cari leftover" ditolak untuk app yang masih terpasang.
5. **Nonaktifkan auto-start = menghapus value Run** (tidak bisa dikembalikan); `desktop.ini` ikut terdaftar.
   *Sekarang:* mekanisme `StartupApproved` seperti Task Manager (reversibel) + tombol "Aktifkan kembali".
6. **"Hentikan proses" selalu `Kill(entireProcessTree: true)`**; tanpa proteksi proses inti. *Sekarang:* hanya proses
   itu (coba tutup sopan dulu); csrss/lsass/wininit/winlogon/services/svchost/dwm dll. ditolak; PID dicocokkan
   dengan nama + waktu mulai agar tidak salah sasaran.
7. Layanan Windows bisa dinonaktifkan tanpa proteksi. *Sekarang:* hanya layanan pihak ketiga yang ditampilkan;
   daftar layanan keamanan dilindungi; selalu minta konfirmasi.
8. **Rencana daya tertukar:** "Performa tinggi" memakai `SCHEME_MAX` (= power saver) dan "Hemat baterai" memakai
   `SCHEME_MIN` (= high performance). Sudah ditukar.

## 3. Bug fungsional

- Path "Error Reports" salah (`Microsoft\Microsoft\Windows\WER`) — diperbaiki.
- "Temporary Windows" = `GetTempPath()` = "Temporary User" (ganda) — sekarang `C:\Windows\Temp` terpisah, deduplikasi.
- Semua scan non-rekursif (isi subfolder Temp tak tersentuh) dan cache browser dicari dengan pola `Cache*` pada
  *nama file* — kini rekursif, cache dicari di folder `Cache`/`Code Cache`/`GPUCache` tiap profil, file <1 hari
  untuk Temp dilewati (tidak mengganggu installer yang sedang jalan).
- Folder Downloads memakai known-folder sebenarnya (bisa dipindah pengguna).
- Kategori "Recycle Bin Metadata" (`$Recycle.Bin`) dihapus — berbahaya dan tidak berguna.
- Auto-refresh proses tiap 2 detik mereset pilihan ("Pilih proses terlebih dahulu"), bisa bertumpuk, dan terus
  berjalan saat window disembunyikan — sekarang pilihan + scroll dipertahankan, ada guard reentrancy, timer hanya
  aktif saat tab Proses terlihat.
- Dashboard memanggil `GetProcesses` 2x dan PowerShell berat hanya untuk menghitung auto-start; "ruang kosong"
  menjumlah semua drive termasuk jaringan/USB — sekarang drive lokal saja; satu panggilan; skor kesehatan dihitung
  dari memori, disk sistem, auto-start, proses (bukan sekadar jumlah proses).
- Label **"ADMINISTRATOR" hardcoded** (selalu hijau) — kini mendeteksi hak sebenarnya + tombol "Jalankan sebagai
  Administrator".
- Tombol "Sembunyikan menu" ada *di dalam* sidebar sehingga menu tak bisa dimunculkan lagi — kini toggle di header.
- Tray tidak berfungsi (menutup window membuang ikon dan menghentikan app) — kini menyembunyikan ke tray,
  single-instance, ikon di-dispose.
- `PowerShellService`: stdout lalu stderr dibaca berurutan (potensi deadlock), encoding OEM vs UTF-8, tanpa timeout,
  gagal-uninstall dideteksi dari kata "error". Kini dibaca paralel, UTF-8, timeout + kill, exit code nyata.
- `sfc` (output UTF-16) berantakan; `Get-Counter` bergantung bahasa Windows; "Baterai" menampilkan HTML mentah;
  deskripsi tidak cocok dengan perintah; filter BSOD terlalu longgar — semuanya diperbaiki.
- Duplikat: meng-hash semua file; crash di folder tak bisa diakses — kini kelompok ukuran dulu, enumerasi
  `IgnoreInaccessible`, ada tombol Batal; tombol "Cari file duplikat" di tab Pembersihan kini membuka tab Tools.
- Jadwal: `schtasks` sinkron di UI thread tiap perubahan, gagal diam-diam, selalu "tersimpan" — kini di background,
  hasil nyata ditampilkan, pilihan dikembalikan bila gagal; cleanup terjadwal tidak menyentuh browser/Downloads.
- Uninstall: path tanpa kutip bersepasi gagal dipecah; `MsiExec /I` (modify) seharusnya `/X`; daftar Store app
  memuat paket framework/non-removable; penutupan proses berbasis kemiripan nama → kini berbasis folder instalasi.
- Entri HKCU\Run muncul ganda (view 64/32) — dideduplikasi.
- Operasi berat di UI thread (kill, bersih-bersih, hapus leftover, DriveInfo) — dipindah ke background.
- Setting mati dihapus: "Ikuti tema Windows", "Tawarkan Restore Point", "Izinkan reputasi online" (tidak pernah
  dipakai). `HideSystemProcesses` kini benar-benar diterapkan. Pengecualian folder memakai batas folder yang benar.
- Kolom "Tipe" menampilkan True/False — kini "Store"/"Desktop". Settings ditulis atomik.

## 4. Perubahan perilaku yang perlu Anda ketahui
- Uninstall, hapus registry, dan matikan layanan **selalu** meminta konfirmasi (walau konfirmasi dimatikan).
- File yang dibersihkan masih masuk Recycle Bin (sesuai desain asli); ruang disk bebas setelah dikosongkan.
- RunOnce tidak lagi dipindai (item sekali-jalan). Layanan bawaan Windows tidak ditampilkan di tab Auto-start.
- Lokasi pengaturan lama (`%APPDATA%\ZidWindowsApps\settings.json`) dipertahankan agar kompatibel.

## 5. Struktur & build
UI dimuat saat runtime dari `Resources/*.xaml` (tertanam di DLL) sehingga bisa dibuild satu perintah di Windows,
Linux, atau CI tanpa task XAML Windows. Kode dipisah: Infrastructure / Models / Services / UI / Resources.
Windows: `./build.ps1`. Linux/CI: `build-linux.sh` (butuh DLL referensi di `refs/`).

## 6. Batas verifikasi (jujur)
- Dikompilasi tanpa error/warning dan diperiksa silang (semua `x:Name` di XAML cocok dengan kode; installer NSIS
  berhasil dibangun dan isinya diverifikasi). **Belum pernah dijalankan di Windows**: lingkungan saya Linux.
  Kegagalan memuat XAML akan menampilkan dialog berisi pesan error (dan tercatat di log), bukan crash diam.
- Launcher `ZidWindowsApps.exe` (native, dibangun di Linux) hanya x64. Untuk ARM64 gunakan `build.ps1` di Windows
  (apphost bawaan SDK).
- Installer belum diuji di Windows; kode `InstallerForm` (bootstrapper asli) tidak saya analisis karena digantikan.
- Butuh **.NET Desktop Runtime 8+**; installer memeriksa dan menawarkan halaman unduhan resmi.
- Beberapa fitur butuh Administrator (Windows Update cache, HKLM, SFC, Set-Service, Get-AppxPackage -AllUsers).
