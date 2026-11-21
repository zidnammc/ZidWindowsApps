using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace ZidWindowsApps;

public static class StartupService
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    // Layanan keamanan yang tidak boleh dimatikan lewat aplikasi ini.
    private static readonly HashSet<string> ProtectedServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "WinDefend", "Sense", "WdNisSvc", "SecurityHealthService", "mpssvc", "BFE", "EventLog", "RpcSs", "DcomLaunch",
    };

    public static List<StartupItem> GetItems(bool includeServices = true)
    {
        var items = new List<StartupItem>();
        AddRegistry(items, RegistryHive.CurrentUser, RegistryView.Default, false, false, "Run");
        AddRegistry(items, RegistryHive.LocalMachine, RegistryView.Registry64, true, false, "Run");
        AddRegistry(items, RegistryHive.LocalMachine, RegistryView.Registry32, true, true, "Run32");
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), false);
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), true);
        if (includeServices) AddServices(items);

        // HKCU\...\Run tidak di-redirect WOW64, jadi bisa muncul ganda: buang duplikat.
        return items
            .GroupBy(i => $"{i.Kind}|{i.Location}|{i.ValueName}|{i.Command}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.Kind).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string ApprovedName(StartupItem item) => item.Kind switch
    {
        StartupKind.StartupFolder => "StartupFolder",
        _ => item.Is32BitView ? "Run32" : "Run",
    };

    private static RegistryKey OpenBase(bool machine, bool is32) =>
        RegistryKey.OpenBaseKey(machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
            is32 ? RegistryView.Registry32 : (machine ? RegistryView.Registry64 : RegistryView.Default));

    private static bool ReadEnabled(bool machine, bool is32, string approvedKind, string valueName)
    {
        try
        {
            using var baseKey = OpenBase(machine, false);   // StartupApproved selalu di view native
            using var key = baseKey.OpenSubKey(ApprovedPath + approvedKind);
            if (key?.GetValue(valueName) is byte[] data && data.Length > 0)
                return (data[0] & 1) == 0;                   // 02/06 = aktif, 03/07 = nonaktif
        }
        catch { }
        return true;
    }

    private static void AddRegistry(List<StartupItem> items, RegistryHive hive, RegistryView view, bool machine, bool is32, string approvedKind)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(RunPath);
            if (key == null) return;
            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrEmpty(name)) continue;
                items.Add(new StartupItem
                {
                    Name = name,
                    Command = Convert.ToString(key.GetValue(name)) ?? "",
                    Location = (machine ? "HKLM" : "HKCU") + (is32 ? " (32-bit)" : "") + "\\Run",
                    Kind = StartupKind.RegistryRun,
                    IsMachineWide = machine,
                    Is32BitView = is32,
                    SubKey = RunPath,
                    ValueName = name,
                    IsEnabled = ReadEnabled(machine, is32, approvedKind, name),
                });
            }
        }
        catch { }
    }

    private static void AddFolder(List<StartupItem> items, string path, bool machine)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.EnumerateFiles(path))
            {
                var name = Path.GetFileName(file);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(file) & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                items.Add(new StartupItem
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    Location = machine ? "Folder Startup (semua pengguna)" : "Folder Startup",
                    Kind = StartupKind.StartupFolder,
                    IsMachineWide = machine,
                    ValueName = name,
                    IsEnabled = ReadEnabled(machine, false, "StartupFolder", name),
                });
            }
        }
        catch { }
    }

    private static void AddServices(List<StartupItem> items)
    {
        try
        {
            var r = PowerShellService.Run(
                "Get-CimInstance Win32_Service | Where-Object { $_.StartMode -in @('Auto','Disabled') } | Select-Object Name,PathName,StartMode | ConvertTo-Json -Compress", 60);
            if (!r.Success || string.IsNullOrWhiteSpace(r.Output)) return;

            using var doc = JsonDocument.Parse(r.Output);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            foreach (var e in elements)
            {
                string Get(string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                var name = Get("Name"); var path = Get("PathName"); var mode = Get("StartMode");
                if (name.Length == 0 || path.Length == 0 || ProtectedServices.Contains(name)) continue;

                // Hanya layanan pihak ketiga; layanan bawaan Windows disembunyikan agar tidak ikut dimatikan tak sengaja.
                var exe = path.StartsWith('"') ? path.Trim().Split('"')[1] : path.Split(' ')[0];
                if (PathGuard.IsUnder(exe, windows)) continue;

                items.Add(new StartupItem
                {
                    Name = name,
                    Command = path,
                    Location = "Windows Service",
                    Impact = "Layanan",
                    Kind = StartupKind.WindowsService,
                    ValueName = name,
                    IsEnabled = !mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase),
                });
            }
        }
        catch { }
    }

    /// <summary>Aktif/nonaktifkan secara REVERSIBEL (mekanisme yang sama dengan Task Manager). Tidak ada yang dihapus.</summary>
    public static void SetEnabled(StartupItem item, bool enabled)
    {
        if (item.Kind == StartupKind.WindowsService)
        {
            if (ProtectedServices.Contains(item.ValueName)) throw new InvalidOperationException("Layanan ini dilindungi.");
            var type = enabled ? "Automatic" : "Disabled";
            var r = PowerShellService.Run($"$ErrorActionPreference='Stop'; Set-Service -Name {PowerShellService.Quote(item.ValueName)} -StartupType {type}", 60);
            if (!r.Success) throw new InvalidOperationException(string.IsNullOrWhiteSpace(r.Combined) ? "Set-Service gagal (butuh Administrator?)." : r.Combined);
            item.IsEnabled = enabled;
            return;
        }

        try
        {
            using var baseKey = OpenBase(item.IsMachineWide, false);
            using var key = baseKey.CreateSubKey(ApprovedPath + ApprovedName(item), writable: true)
                            ?? throw new InvalidOperationException("Tidak dapat membuka key StartupApproved.");
            var data = new byte[12];
            if (enabled) data[0] = 0x02;
            else
            {
                data[0] = 0x03;
                BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            }
            key.SetValue(item.ValueName, data, RegistryValueKind.Binary);
            item.IsEnabled = enabled;
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Item ini berlaku untuk semua pengguna; jalankan aplikasi sebagai Administrator.");
        }
    }
}
