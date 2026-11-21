# Migrasi WPF native

1. Simpan cadangan, identifikasi kontrak layanan dan rekam UI lama.
2. Muat shell XAML dan token ResourceDictionary lewat jalur Debug --new-ui. Pertahankan entry point dan UI rilis selama peninjauan.
3. Bangun navigasi berkelompok, palet Ctrl+K, tema, komponen tabel, kartu dan dialog WPF. Gunakan kontrol keyboard native serta nama aksesibilitas.
4. Port sembilan layar dari TSX. Jalankan interaksi pada fixture lokal, tangkap kedua tema, dan tinjau pada GATE-B.
5. Setelah persetujuan visual, hubungkan per layar ke layanan dalam AUDIT.md. Konfirmasi, pembatalan dan hasil parsial diuji memakai fixture atau VM.
6. Uji navigasi keyboard, penskalaan, konsumsi memori dan start sebelum membuat installer.
7. Tinjau build dan rollback pada GATE-C sebelum mengganti default.
