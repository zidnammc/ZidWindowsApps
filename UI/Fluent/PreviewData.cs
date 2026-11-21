using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace ZidWindowsApps.Fluent;
internal sealed record ProcessRow(int Pid, string Name, double Cpu, double RamMb, string Status, string Publisher, string Path, bool IsSystem, int? ParentPid)
{
    public bool Protected => IsSystem || Name.Equals("ZidWindowsApps.exe", StringComparison.OrdinalIgnoreCase);
    public string Memory => RamMb < 1 ? (RamMb * 1024).ToString("N0", PreviewData.Locale) + " KB" : PreviewData.Size(RamMb);
}

internal sealed record StartupRow(string Name, bool Enabled, string Location, string Command)
{
    public string Status => Enabled ? "Aktif" : "Nonaktif";
}

internal sealed record Residue(string Kind, string Path, string Size);
internal sealed record AppRow(string Name, string Version, string Publisher, string Type, string Size, string InstallDate, string Location, List<Residue> Leftovers);
internal sealed record CacheRow(string Category, int Files, double SizeMb, string SizeLabel, string Path, bool IsBrowser)
{
    public bool Selected { get; set; } = true;
}

internal sealed record Diagnostic(string Title, string Desc, bool RequiresAdmin, string Risk, List<string> Logs);
internal sealed record PreviewData(List<ProcessRow> Processes, List<StartupRow> Startup, List<AppRow> Apps, List<CacheRow> Cleanup, List<Diagnostic> Tools)
{
    internal static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream("ZidWindowsApps.Fluent." + name) ?? throw new InvalidDataException("Resource pratinjau hilang: " + name);
    internal static PreviewData Load()
    {
        using var file = Resource("PreviewData.json");
        return JsonSerializer.Deserialize<PreviewData>(file, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Data pratinjau kosong.");
    }

    internal static string Size(double mb) => mb >= 1024 ? (mb / 1024).ToString("N2", Locale) + " GB" : mb.ToString("N2", Locale) + " MB";
    internal static readonly CultureInfo Locale = CultureInfo.GetCultureInfo("id-ID");
    internal List<ProcessRow> ProcessTree(ProcessRow root)
    {
        var found = new List<ProcessRow>
        {
            root
        };
        for (int i = 0; i < found.Count; i++)
            found.AddRange(Processes.Where(p => p.ParentPid == found[i].Pid && !found.Contains(p)).ToList());
        return found;
    }
}

internal sealed class PreviewPreferences
{
    public bool Dark { get; set; }
    public bool Collapsed { get; set; }
    public bool Tray { get; set; } = true;
    public bool HideSystem { get; set; } = true;
    public bool Confirm { get; set; } = true;
    public bool Schedule { get; set; }
    public string Frequency { get; set; } = "Mingguan";
    public List<string> Exclusions { get; set; } = new();
    internal static string FileName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZidWindowsApps", "Preview", "preferences.json");

    internal static PreviewPreferences Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<PreviewPreferences>(File.ReadAllText(FileName)) ?? new();
            settings.Exclusions ??= new();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
        var temp = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(this));
            File.Move(temp, FileName, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
