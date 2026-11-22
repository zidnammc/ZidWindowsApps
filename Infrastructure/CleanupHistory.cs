using System;
using System.IO;
using System.Text.Json;

namespace ZidWindowsApps;

public sealed record LastCleanup(DateTime When, int Files, long Bytes, bool Scheduled);

/// <summary>Ringkasan pembersihan terakhir, disimpan terpisah dari pengaturan agar mode terjadwal tidak menimpa settings.json.</summary>
public static class CleanupHistory
{
    public static LastCleanup? Load()
    {
        try
        {
            return File.Exists(AppPaths.LastCleanupFile) ? JsonSerializer.Deserialize<LastCleanup>(File.ReadAllText(AppPaths.LastCleanupFile)) : null;
        }
        catch { return null; }
    }

    public static LastCleanup? Record(CleanupResult result, bool scheduled)
    {
        if (result.Deleted == 0) return null;
        var entry = new LastCleanup(DateTime.Now, result.Deleted, result.FreedBytes, scheduled);
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var temporaryFile = AppPaths.LastCleanupFile + ".tmp";
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(entry));
            File.Move(temporaryFile, AppPaths.LastCleanupFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return entry;
    }
}
