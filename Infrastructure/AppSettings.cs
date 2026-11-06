using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ZidWindowsApps;

public sealed class AppSettings
{
    public string Language { get; set; } = "id";
    public bool HideSystemProcesses { get; set; } = true;
    public bool ConfirmBeforeAction { get; set; } = true;
    public bool EnableTray { get; set; } = true;
    public bool ScheduledCleanupEnabled { get; set; }
    public int LogRetentionDays { get; set; } = 60;

    /// <summary>Harian | Mingguan | Bulanan (nilai lama dipertahankan agar kompatibel).</summary>
    public string CleanupFrequency { get; set; } = "Mingguan";

    public List<string> ExcludedPaths { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new AppSettings();
                s.ExcludedPaths ??= new List<string>();
                if (s.LogRetentionDays < 1) s.LogRetentionDays = 60;
                return s;
            }
        }
        catch { /* file rusak -> pakai default */ }
        return new AppSettings();
    }

    /// <summary>Menyimpan secara atomik (tulis ke file sementara lalu timpa). Mengembalikan false bila gagal.</summary>
    public bool Save(out string? error)
    {
        try
        {
            var dir = Path.GetDirectoryName(AppPaths.SettingsFile)!;
            Directory.CreateDirectory(dir);
            var tmp = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, AppPaths.SettingsFile, overwrite: true);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
