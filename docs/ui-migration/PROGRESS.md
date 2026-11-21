# Tinjauan pratinjau WPF · 21 November 2026

**Riwayat pratinjau GATE-B.** Desain telah disetujui dan diterapkan di versi 1.2.0. Lihat [laporan rilis](REPORT.md) dan [hasil QA](QA.md). Buka **Buka Pratinjau Fluent.lnk** di folder ini. Bila sumber dipindah ke mesin lain, jalankan `dotnet run --project ZidWindowsApps.csproj -c Debug -- --new-ui` dari akar proyek. Ctrl+K membuka pencarian, ↑/↓ dan Enter memilih modul, Esc menutup dialog. Ctrl+Shift+G membuka galeri komponen dan contoh status memuat, kosong, serta gagal/coba lagi.

| Layar | UI lama | Tema terang | Tema gelap |
|---|---|---|---|
| Dashboard | [Sebelum](before/dashboard.png) | [Terang](preview/light/dashboard.png) | [Gelap](preview/dark/dashboard.png) |
| Processes | [Sebelum](before/processes.png) | [Terang](preview/light/processes.png) | [Gelap](preview/dark/processes.png) |
| Auto-start | [Sebelum](before/autostart.png) | [Terang](preview/light/autostart.png) | [Gelap](preview/dark/autostart.png) |
| Uninstaller | [Sebelum](before/uninstaller.png) | [Terang](preview/light/uninstaller.png) | [Gelap](preview/dark/uninstaller.png) |
| Cleanup | [Sebelum](before/cleanup.png) | [Terang](preview/light/cleanup.png) | [Gelap](preview/dark/cleanup.png) |
| System Info | [Sebelum](before/sysinfo.png) | [Terang](preview/light/sysinfo.png) | [Gelap](preview/dark/sysinfo.png) |
| Troubleshooting | [Sebelum](before/troubleshooting.png) | [Terang](preview/light/troubleshooting.png) | [Gelap](preview/dark/troubleshooting.png) |
| Tools | [Sebelum](before/tools.png) | [Terang](preview/light/tools.png) | [Gelap](preview/dark/tools.png) |
| Settings | [Sebelum](before/settings.png) | [Terang](preview/light/settings.png) | [Gelap](preview/dark/settings.png) |

Gambar adalah render permukaan WPF pada Windows dengan bingkai jendela native di luar gambar. Pratinjau memakai ukuran jendela 1280 × 720. Tangkapan lama menggunakan halaman awal tiap modul, sebelum interaksi pemindaian manual. Tersedia juga [dialog](preview/dialog.png), [palet](preview/palette.png), [galeri](preview/gallery.png), [1024 × 640](preview/compact.png) dan [1366 × 768](preview/wide.png).

### Hasil pengerjaan

- [x] Sumber yang cocok dengan instalasi, cadangan, pemetaan layanan dan keputusan B1. Lihat AUDIT.md dan PLAN-NEW-UI-JALUR-B.md.
- [x] Shell WPF terpisah, tema tersimpan, sidebar 230/68 DIP, status bar, navigasi sembilan layar, palet, dialog dan toast.
- [x] Fixture Debug dari lampiran TSX meliputi 16 proses, 16 auto-start, 12 aplikasi, 8 kategori cache dan 9 diagnostik. Dua proses diberi contoh hubungan induk-anak untuk menguji penghentian tree.
- [x] Pengujian Windows merender 18 tampilan dan memeriksa pencarian PID, batal/konfirmasi proses, pencarian palet, filter browser, pemindaian leftover sesudah uninstall, keadaan awal seleksi leftover, switch auto-start, laporan TXT, pemulihan contoh error, dan pembatalan diagnostik. Runner berada di tools/FluentReview. Dari akar proyek, jalankan `dotnet run --project tools/FluentReview -- docs/ui-migration preview`. Gunakan argumen terakhir `before` untuk render UI lama.
- [x] Debug dan Release berhasil dibangun. Fixture dan entry point pratinjau dikecualikan dari Release. Mode standar, tray dan mode terjadwal memakai jalur lama.
- [ ] Integrasi layanan, pengukuran performa pembanding, audit aksesibilitas dan installer setelah peninjauan visual.

### Perbedaan yang perlu ditinjau

- Ikon memakai Segoe MDL2 Assets dari Windows. Tabel menggunakan DataGrid native ber-virtualisasi dengan urutan kolom, fokus, scrollbar dan pilihan keyboard WPF. Beberapa kolom perlu digulir horizontal. Gaya penanda status, grafik mini per baris dan warna per baris konsol masih lebih sederhana daripada TSX.
- Badge memakai jumlah item fixture yang benar-benar tersedia. Pada Dashboard, skor contoh dihitung dari 100 dikurangi 10 untuk tekanan RAM 88%, hingga 10 poin cache (satu per 512 MB dibulatkan), dan hingga 10 poin untuk jumlah auto-start aktif. Rumus produksi menunggu data nyata. Telemetri status bar, hak admin, tray, jadwal dan rencana daya dalam pratinjau merupakan simulasi.
- Palet mencakup Settings. Operasi berisiko membuka dialog, proses sistem dan aplikasi sendiri dilindungi, leftover menunggu pilihan eksplisit, dan pemindaian file duplikat memakai hasil contoh. Export TXT dan Salin bekerja dengan isi laporan contoh. Preferensi pratinjau memakai file terpisah di %LOCALAPPDATA%\ZidWindowsApps\Preview.
- Rencana daya memakai tombol pilihan native. Pilihan bahasa pratinjau adalah Indonesia. Galeri menyediakan status kegagalan dan pemulihan generik. Status kegagalan per layanan, animasi/progres untuk pemindaian nyata, pemeriksaan pembaca layar dan DPI menunggu integrasi.
- Laporan baterai, Pengaturan Windows, Disk Management, Startup Apps dan Hapus Semua Log ditandai **Belum tersedia** di pratinjau. Dua pintasan pertama dan penghapusan log memiliki handler lama. Pengosongan Recycle Bin dan status keamanan terstruktur belum memiliki kontrak lengkap. Pertahankan kemampuan multi-pilih auto-start, pengecualian dan alur ekspor lama saat menghubungkan layanan.

### Pengukuran awal

Pada AMD 3020e, 2 prosesor logis, RAM terdeteksi sekitar 3,38 GiB, satu peluncuran instalasi lama menghasilkan 1.706 ms hingga handle jendela terdeteksi. Setelah delapan detik, sampel lima detik menghasilkan working set 108,8 MiB dan CPU rata-rata 3,22% dari seluruh kapasitas CPU. Mesin juga sedang menjalankan pekerjaan build. Ini pengukuran awal satu kali. Ulangi beberapa kali tanpa beban build sebelum mengambil keputusan rilis.
