# ZidWindowsApps 1.2.0 · Fluent WPF

Versi 1.2.0 sudah terpasang dan berhasil dibuka melalui C:\Program Files\ZidWindowsApps\ZidWindowsApps.exe. Registrasi uninstall, versi assembly, dan hash DLL telah diverifikasi.

Persetujuan desain B1 diterapkan pada aplikasi utama. Fluent kini memakai layanan Windows pada sembilan modul, hak akses token proses, tema dan sidebar persisten, serta pengaturan lokal yang kompatibel dengan versi 1.1.0. Data demonstrasi hanya digunakan oleh jalur pratinjau Debug.

Dashboard menampilkan skor dari penggunaan memori, ruang bebas drive sistem, jumlah auto-start dan proses. Ringkasan cleanup baru muncul setelah pemindaian. Daftar daya hanya memuat skema yang tersedia. Opsi copy/export menulis laporan perangkat asli, dan perubahan jadwal menggunakan Task Scheduler.

Aksi berisiko memerlukan konfirmasi. Pemindaian sisa aplikasi ditawarkan sesudah uninstall berhasil. File Downloads perlu dipilih manual. Pencari duplikat mempertahankan satu file asli dan memverifikasi kembali hash sebelum memindahkan salinan. Setelah memindahkan file cleanup, pengguna dapat memilih untuk mengosongkan Recycle Bin drive sistem melalui konfirmasi terpisah.

### Distribusi dan pemulihan

Installer Windows x64 self-contained berada di `bin/packages/1.2.0/ZidWindowsApps-Setup-1.2.0-x64.exe`. Paket berukuran 49.707.133 byte dengan SHA-256 `ED99918E2CF4A88E6D1FDFF1660E477598DB4BFBC3D1D02DBA55AB4615C0A9B8`. Assembly hasil publish memiliki versi 1.2.0.0 dan SHA-256 `7E044F0E8BA5755A03AF8C272D1BC5085EA386D06616137DEB67BE4515C5F3BB`.

Program juga dapat dijalankan langsung dari subfolder `win-x64`; seluruh isi folder diperlukan. Installer memperbarui entri aplikasi dan pintasan Windows.

Untuk menggunakan UI sebelumnya, keluar dari ZidWindowsApps melalui tray, kemudian jalankan `ZidWindowsApps.exe --legacy-ui`. Salinan proyek dan hasil distribusi 1.1.0 yang cocok dengan instalasi sebelumnya tersimpan di lokasi pada AUDIT.md.

Hasil pengujian, screenshot kedua tema, pengukuran performa dan cakupan yang belum diverifikasi dijelaskan di QA.md.
