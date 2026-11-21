# QA Fluent WPF 1.2.0

Diuji pada Windows x64, AMD 3020e dengan dua prosesor logis dan RAM sekitar 3,38 GiB. Aplikasi membuka Fluent lewat entry point normal; opsi `--legacy-ui` membuka antarmuka lama. Kedua mode berhasil menampilkan jendela dari distribusi self-contained.

| Pemeriksaan | Hasil |
|---|---|
| Dashboard, Processes, Auto-start, Uninstaller, Cleanup, System Info, Troubleshooting, Tools dan Settings | Sembilan rute berhasil dirender dengan layanan Windows. Bukti pada folder live dan live-dark. |
| Palet perintah dan konfirmasi | Palet dapat dibuka dan ditutup. Batal pada dialog penghapusan log mengembalikan fokus tanpa menjalankan penghapusan. |
| Proses | Daftar nyata dapat difilter, proses aplikasi sendiri dilindungi. PID dan waktu mulai diperiksa oleh ProcessService sebelum menghentikan proses. |
| Duplikat dan cleanup | Dua file di folder sementara di-hash. Salinan yang berubah dilewati, salinan identik dipindahkan ke Recycle Bin, file asli bertahan. Pengecualian folder mencegah pembersihan file fixture. |
| Registry | Key HKCU khusus pengujian diekspor, nilai penanda ditemukan di .reg, penghapusan key diverifikasi. Artefak fixture dibersihkan. |
| System Info | TXT berisi data snapshot. Produksi menggunakan dialog simpan dan generator laporan yang sama. |
| Power plan | Nama dan pilihan aktif dibaca dari powercfg pada Windows lokal. |
| Paket | Data fixture Debug dikecualikan dari assembly Release. XAML Fluent tersedia di assembly. |
| Pengujian pratinjau | Suite interaksi pratinjau lulus sesudah shell dipakai bersama. |

Perintah reproduksi dari akar proyek:

```powershell
dotnet run --project tools/FluentReview -c Release -- docs/ui-migration live
dotnet run --project tools/FluentReview -c Debug -- "$env:TEMP\ZidWindowsApps-review" preview
```

### Pengukuran pada mesin acuan

- UI lama v1.1.0 pada audit awal mencatat 1.706 ms hingga jendela muncul, working set 108,8 MiB dan CPU 3,22% selama lima detik.
- Pada pengujian build Fluent, animasi bawaan progress bar WPF menyumbang beban idle. Dashboard kini memakai batang statis yang berubah saat ada sampel telemetri baru. Tiga sampel lima detik pada build akhir mencatat CPU 0,78%, 0,93%, dan 0,31%; working set 120,4–125,8 MiB. Jendela muncul dalam 2.948 ms saat kompresi installer berjalan.
- CPU pada tiga sampel memenuhi sasaran lunak 3%. Pengukuran awal RAM dan waktu buka melewati toleransi perbandingan 10%; beban kompresi membuat pengukuran waktu buka kurang sebanding. Polling dashboard dan proses berhenti saat jendela tersembunyi atau diminimalkan.

### Instalasi di perangkat

Installer selesai dengan kode 0. Versi di registry menjadi 1.2.0 dan SHA-256 DLL pada Program Files sama dengan hasil publish. Aplikasi dibuka dari Program Files tanpa argumen. Waktu hingga jendela muncul 2.613 ms. Sepuluh detik sesudah pemanasan awal mencatat CPU 7,09%; tiga sampel berikutnya 1,24%, 0,47% dan 0,62% dengan RAM 120,9–121,3 MiB. Waktu buka dan RAM masih di atas sasaran perbandingan 10% terhadap baseline audit.

### Lingkup verifikasi

Uji mesin utama mencakup pembacaan perangkat, pemilihan, pembatalan, dan operasi terisolasi pada fixture. Uninstall aplikasi pihak ketiga, pengosongan Recycle Bin, perubahan layanan/jadwal sistem dan perbaikan SFC/DISM belum dijalankan end-to-end di VM. Pemeriksaan pembaca layar dan seluruh variasi DPI juga belum dilakukan.
