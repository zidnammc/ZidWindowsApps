using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ZidWindowsApps;

public static class SystemInfoService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private static MemoryStatusEx ReadMemory()
    {
        var m = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        GlobalMemoryStatusEx(ref m);
        return m;
    }

    public static int GetMemoryLoadPercent() => (int)ReadMemory().MemoryLoad;

    /// <summary>Drive lokal (fixed) saja; drive jaringan/removable/CD tidak ikut dihitung.</summary>
    public static List<(string Name, long Total, long Free)> GetFixedDrives()
    {
        var list = new List<(string, long, long)>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                list.Add((d.Name, d.TotalSize, d.AvailableFreeSpace));
            }
            catch { /* drive terlepas saat dibaca */ }
        }
        return list;
    }

    private static string WindowsVersionText()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName") as string ?? "Windows";
            var display = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "";
            int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out var build);
            // ProductName di registry masih "Windows 10 ..." pada Windows 11.
            if (build >= 22000) product = product.Replace("Windows 10", "Windows 11");
            var ubr = key?.GetValue("UBR");
            return $"{product} {display} (build {build}{(ubr != null ? "." + ubr : "")}, {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})".Replace("  ", " ");
        }
        catch { return Environment.OSVersion.ToString(); }
    }

    private static string CpuText()
    {
        string name = "";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            name = (key?.GetValue("ProcessorNameString") as string ?? "").Trim();
        }
        catch { }
        return $"{(name.Length > 0 ? name + " - " : "")}{Environment.ProcessorCount} thread logis, {RuntimeInformation.OSArchitecture}";
    }

    private static string StorageText()
    {
        var drives = GetFixedDrives();
        return drives.Count == 0 ? "Tidak ada drive lokal terbaca" :
            string.Join(Environment.NewLine, drives.Select(d => $"{d.Name}  {Format.Bytes(d.Total)} total, {Format.Bytes(d.Free)} kosong"));
    }

    private static string MemoryText()
    {
        var m = ReadMemory();
        return $"{Format.Bytes((long)m.TotalPhys)} total, {Format.Bytes((long)m.AvailPhys)} tersedia ({m.MemoryLoad}% terpakai)";
    }

    private static List<JsonElement> JsonItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<JsonElement>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList()
                : new List<JsonElement> { doc.RootElement.Clone() };
        }
        catch { return new List<JsonElement>(); }
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";

    private static async Task<string> QueryLinesAsync(string script, Func<JsonElement, string> format, string empty)
    {
        try
        {
            var r = await PowerShellService.RunAsync(script + " | ConvertTo-Json -Compress", 90).ConfigureAwait(false);
            var items = JsonItems(r.Output);
            return items.Count == 0 ? empty : string.Join(Environment.NewLine, items.Select(format));
        }
        catch (Exception ex) { return "Tidak dapat dibaca: " + ex.Message; }
    }

    public static async Task<SystemSnapshot> GetSnapshotAsync()
    {
        // Semua query PowerShell dijalankan paralel (sebelumnya berurutan => 6x lebih lama).
        var graphics = QueryLinesAsync("Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion",
            e => $"{Str(e, "Name")} (driver {Str(e, "DriverVersion")})", "Tidak terdeteksi");
        var battery = QueryLinesAsync("Get-CimInstance Win32_Battery | Select-Object EstimatedChargeRemaining,BatteryStatus",
            e => $"Terisi {Str(e, "EstimatedChargeRemaining")}% (status {Str(e, "BatteryStatus")})", "Tidak ada baterai terdeteksi");
        var network = QueryLinesAsync("Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object Status -eq 'Up' | Select-Object Name,InterfaceDescription,LinkSpeed",
            e => $"{Str(e, "Name")}: {Str(e, "InterfaceDescription")} ({Str(e, "LinkSpeed")})", "Tidak ada adapter jaringan aktif");
        var firmware = QueryLinesAsync("Get-CimInstance Win32_BIOS | Select-Object Manufacturer,SMBIOSBIOSVersion,ReleaseDate",
            e => $"{Str(e, "Manufacturer")} {Str(e, "SMBIOSBIOSVersion")} ({Str(e, "ReleaseDate")})", "Tidak terbaca");
        var drivers = QueryLinesAsync("[pscustomobject]@{ Unsigned = @(Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.IsSigned -eq $false }).Count }",
            e => $"Driver tidak bertanda tangan: {Str(e, "Unsigned")}", "Tidak terbaca");
        var security = SecurityTextAsync();

        await Task.WhenAll(graphics, battery, network, firmware, drivers, security).ConfigureAwait(false);

        return new SystemSnapshot
        {
            OperatingSystem = WindowsVersionText(),
            ComputerName = Environment.MachineName,
            Processor = CpuText(),
            Memory = MemoryText(),
            Storage = StorageText(),
            Graphics = graphics.Result,
            Battery = battery.Result,
            Network = network.Result,
            Firmware = firmware.Result,
            Drivers = drivers.Result,
            Security = security.Result,
        };
    }

    private static async Task<string> SecurityTextAsync()
    {
        var script = @"
$tpm = try { (Get-Tpm -ErrorAction Stop).TpmPresent } catch { $null }
$sb  = try { Confirm-SecureBootUEFI -ErrorAction Stop } catch { $null }
$bl  = try { (Get-BitLockerVolume -MountPoint $env:SystemDrive -ErrorAction Stop).ProtectionStatus } catch { $null }
[pscustomobject]@{ Tpm=$tpm; SecureBoot=$sb; BitLocker=$bl } | ConvertTo-Json -Compress";
        try
        {
            var r = await PowerShellService.RunAsync(script, 60).ConfigureAwait(false);
            var e = JsonItems(r.Output).FirstOrDefault();
            if (e.ValueKind == JsonValueKind.Undefined) return "Tidak terbaca";
            string T(string name, string tVal, string fVal)
            {
                if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return Admin.IsAdministrator ? "tidak tersedia" : "butuh Administrator / tidak didukung";
                return v.ValueKind == JsonValueKind.True ? tVal : v.ValueKind == JsonValueKind.False ? fVal : v.ToString();
            }
            var bit = e.TryGetProperty("BitLocker", out var bv) && bv.ValueKind != JsonValueKind.Null
                ? (bv.ToString() is "1" or "On" ? "aktif" : "nonaktif") : (Admin.IsAdministrator ? "tidak tersedia" : "butuh Administrator");
            return $"TPM: {T("Tpm", "ada", "tidak ada")}; Secure Boot: {T("SecureBoot", "aktif", "nonaktif")}; BitLocker (drive sistem): {bit}";
        }
        catch (Exception ex) { return "Tidak dapat dibaca: " + ex.Message; }
    }

    public static async Task ExportReportAsync(SystemSnapshot snapshot, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ZidWindowsApps - Laporan Sistem");
        sb.AppendLine($"Dibuat: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 48));
        foreach (var p in typeof(SystemSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            sb.AppendLine();
            sb.AppendLine("[" + p.Name + "]");
            sb.AppendLine(Convert.ToString(p.GetValue(snapshot)));
        }
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(true)).ConfigureAwait(false);
    }
}
