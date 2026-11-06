using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ZidWindowsApps;

public static class Format
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Bytes(long bytes)
    {
        double v = Math.Max(0, bytes);
        int i = 0;
        while (v >= 1024 && i < Units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {Units[i]}";
    }
}

public sealed class ProcessInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public long MemoryBytes { get; init; }
    public string Memory => Format.Bytes(MemoryBytes);
    public double Cpu { get; init; }
    public string Path { get; init; } = "";
    public string Publisher { get; init; } = "";
    public DateTime? StartTime { get; init; }
    public bool IsSystem { get; init; }

    /// <summary>Path tidak terbaca (proses dilindungi / milik sesi lain).</summary>
    public bool IsProtected => string.IsNullOrEmpty(Path);

    public string Classification =>
        IsSystem ? "Sistem Windows" :
        IsProtected ? "Dilindungi" :
        !string.IsNullOrWhiteSpace(Publisher) ? "Aplikasi" : "Tidak dikenal";
}

public sealed class InstalledApp
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public string UninstallString { get; init; } = "";
    public bool IsStoreApp { get; init; }
    public string PackageFullName { get; init; } = "";

    /// <summary>Untuk aplikasi Win32: lokasi key registry agar keberadaannya bisa diperiksa ulang.</summary>
    public string RegistryView { get; init; } = "";
    public string RegistrySubKey { get; init; } = "";

    public string TypeText => IsStoreApp ? "Store" : "Desktop";
    public string Display => string.IsNullOrWhiteSpace(Version) ? Name : $"{Name} ({Version})";
}

public enum StartupKind { RegistryRun, StartupFolder, WindowsService }

public sealed class StartupItem
{
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public string Location { get; init; } = "";
    public string Impact { get; init; } = "Tidak diketahui";
    public StartupKind Kind { get; init; }
    public bool IsEnabled { get; set; } = true;
    public string StatusText => IsEnabled ? "Aktif" : "Nonaktif";

    // Identitas teknis (menggantikan string "a|b|c" yang rapuh)
    public bool IsMachineWide { get; init; }         // HKLM / folder Startup semua pengguna
    public bool Is32BitView { get; init; }
    public string SubKey { get; init; } = "";        // path key Run
    public string ValueName { get; init; } = "";     // nama value / nama file / nama service
}

public sealed record ServiceEntry(string Name, string Path, string StartMode);

public sealed class CleanupItem : INotifyPropertyChanged
{
    private bool _selected = true;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Category { get; init; } = "";
    public string Path { get; init; } = "";
    public long Size { get; init; }
    public int FileCount => Files.Count;
    public string SizeText => Format.Bytes(Size);
    public List<string> Files { get; init; } = new();

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }
}

public enum LeftoverKind { Folder, File, Registry }

public sealed class LeftoverItem
{
    public LeftoverKind Kind { get; init; }
    public string Target { get; init; } = "";      // path file/folder, atau subkey registry
    public bool IsHklm { get; init; }              // untuk registry
    public bool Is32BitView { get; init; }         // untuk registry
    public string Display => Kind switch
    {
        LeftoverKind.Registry => $"[Registry] {(IsHklm ? "HKLM" : "HKCU")}\\{Target}  (dicadangkan ke .reg lalu dihapus)",
        LeftoverKind.Folder => $"[Folder] {Target}",
        _ => $"[File] {Target}",
    };
}

public sealed class DuplicateGroup
{
    public string Hash { get; init; } = "";
    public long Size { get; init; }
    public List<string> Files { get; init; } = new();
}

public sealed class SystemSnapshot
{
    public string OperatingSystem { get; set; } = "";
    public string ComputerName { get; set; } = "";
    public string Processor { get; set; } = "";
    public string Memory { get; set; } = "";
    public string Graphics { get; set; } = "";
    public string Storage { get; set; } = "";
    public string Battery { get; set; } = "";
    public string Network { get; set; } = "";
    public string Security { get; set; } = "";
    public string Firmware { get; set; } = "";
    public string Drivers { get; set; } = "";
}

public sealed class TroubleItem
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool RequiresAdmin { get; init; }
    public bool ChangesSystem { get; init; }
    public Func<Task<string>> Action { get; init; } = () => Task.FromResult("");
}

public sealed record PowerShellResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
    public string Combined => string.IsNullOrWhiteSpace(Error) ? Output : (string.IsNullOrWhiteSpace(Output) ? Error : Output + Environment.NewLine + Error);
}

public sealed record UninstallOutcome(bool Started, bool Removed, string Message);
