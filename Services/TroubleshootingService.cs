using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZidWindowsApps;

public static class TroubleshootingService
{
    private const string NeedAdmin = "Pemeriksaan ini butuh hak Administrator. Tutup aplikasi, lalu jalankan ulang sebagai Administrator (atau klik 'Jalankan sebagai Administrator' di bagian atas).";

    private static Func<Task<string>> Ps(string script, int timeout = 120, bool admin = false) => async () =>
    {
        if (admin && !Admin.IsAdministrator) return NeedAdmin;
        try
        {
            var r = await PowerShellService.RunAsync(script, timeout).ConfigureAwait(false);
            var text = r.Combined;
            return string.IsNullOrWhiteSpace(text) ? (r.Success ? "Selesai tanpa output." : $"Gagal (kode {r.ExitCode}).") : text;
        }
        catch (Exception ex) { return "Gagal menjalankan perintah: " + ex.Message; }
    };

    public static List<TroubleItem> GetItems() => new()
    {
        new TroubleItem
        {
            Name = "Internet dan DNS",
            Description = "Cek konfigurasi IP, koneksi keluar, dan resolusi DNS.",
            Action = Ps(@"Write-Output '== Konfigurasi jaringan =='
Get-NetIPConfiguration | Format-List InterfaceAlias,IPv4Address,IPv4DefaultGateway,DNSServer | Out-String -Width 200
Write-Output '== Koneksi keluar (ping 1.1.1.1) =='
if (Test-Connection -ComputerName 1.1.1.1 -Count 2 -Quiet) { 'OK: tersambung ke internet' } else { 'GAGAL: tidak ada balasan dari 1.1.1.1' }
Write-Output '== DNS (microsoft.com) =='
try { Resolve-DnsName microsoft.com -ErrorAction Stop | Select-Object -First 3 Name,Type,IPAddress | Format-Table -AutoSize | Out-String -Width 200 } catch { 'GAGAL: ' + $_.Exception.Message }"),
        },
        new TroubleItem
        {
            Name = "Windows Update",
            Description = "Menampilkan status layanan Windows Update, BITS, dan Cryptographic.",
            Action = Ps("Get-Service wuauserv,bits,cryptsvc | Select-Object Name,Status,StartType | Format-Table -AutoSize | Out-String -Width 200"),
        },
        new TroubleItem
        {
            Name = "Performa saat ini",
            Description = "Beban CPU, memori, dan 10 proses terbesar (tidak bergantung bahasa Windows).",
            Action = Ps(@"$cpu = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
$os = Get-CimInstance Win32_OperatingSystem
$total = [math]::Round($os.TotalVisibleMemorySize/1MB,1); $free = [math]::Round($os.FreePhysicalMemory/1MB,1)
Write-Output (""CPU: $cpu %"")
Write-Output (""Memori: $([math]::Round($total-$free,1)) GB terpakai dari $total GB"")
Write-Output ''
Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 10 Name,Id,@{n='Memori(MB)';e={[math]::Round($_.WorkingSet64/1MB)}} | Format-Table -AutoSize | Out-String -Width 200"),
        },
        new TroubleItem
        {
            Name = "Baterai",
            Description = "Ringkasan kondisi baterai. Laporan lengkap (HTML) ada di tab Tools.",
            Action = Ps(@"$b = Get-CimInstance Win32_Battery
if (-not $b) { 'Tidak ada baterai terdeteksi (PC desktop?).' } else { $b | Select-Object EstimatedChargeRemaining,BatteryStatus,EstimatedRunTime | Format-List | Out-String -Width 200 }"),
        },
        new TroubleItem
        {
            Name = "Riwayat Blue Screen",
            Description = "Menampilkan 5 kejadian BugCheck terakhir dari Event Log.",
            Action = Ps(@"$e = Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Microsoft-Windows-WER-SystemErrorReporting'; Id=1001} -MaxEvents 5 -ErrorAction SilentlyContinue
if (-not $e) { 'Tidak ada catatan blue screen.' } else { $e | ForEach-Object { $_.TimeCreated.ToString('yyyy-MM-dd HH:mm') + '  ' + ($_.Message -split ""`n"")[0] } }"),
        },
        new TroubleItem
        {
            Name = "Bersihkan cache DNS",
            Description = "Menjalankan ipconfig /flushdns. Aman dan tidak butuh restart.",
            ChangesSystem = true,
            Action = Ps("ipconfig /flushdns"),
        },
        new TroubleItem
        {
            Name = "Periksa file sistem (SFC)",
            Description = "sfc /scannow. Bisa memakan beberapa menit. Butuh Administrator.",
            RequiresAdmin = true,
            ChangesSystem = true,
            Action = Ps("sfc /scannow | ForEach-Object { ($_ -replace \"`0\",'').Trim() } | Where-Object { $_ }", 1800, admin: true),
        },
        new TroubleItem
        {
            Name = "Periksa komponen Windows (DISM)",
            Description = "DISM /CheckHealth: hanya memeriksa, tidak memperbaiki. Butuh Administrator.",
            RequiresAdmin = true,
            Action = Ps("DISM /Online /Cleanup-Image /CheckHealth", 600, admin: true),
        },
        new TroubleItem
        {
            Name = "Reset jaringan (winsock & IP)",
            Description = "Mengatur ulang stack jaringan. Butuh Administrator dan restart Windows setelahnya.",
            RequiresAdmin = true,
            ChangesSystem = true,
            Action = Ps("netsh winsock reset; netsh int ip reset", 120, admin: true),
        },
    };
}
