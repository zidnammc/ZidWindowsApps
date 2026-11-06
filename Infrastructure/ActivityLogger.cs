using System;
using System.IO;

namespace ZidWindowsApps;

public sealed class ActivityLogger
{
    private readonly object _gate = new();
    private string CurrentFile => Path.Combine(AppPaths.LogsDirectory, $"activity-{DateTime.Now:yyyy-MM-dd}.log");

    public void Write(string message)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(AppPaths.LogsDirectory);
                File.AppendAllText(CurrentFile, $"{DateTime.Now:O} | {message}{Environment.NewLine}");
            }
        }
        catch { /* logging tidak boleh menjatuhkan aplikasi */ }
    }

    public void CleanupOldLogs(int retentionDays)
    {
        try
        {
            if (!Directory.Exists(AppPaths.LogsDirectory)) return;
            var limit = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
            foreach (var file in Directory.EnumerateFiles(AppPaths.LogsDirectory, "*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < limit) File.Delete(file);
            }
        }
        catch { }
    }

    public bool ClearAll()
    {
        try
        {
            if (Directory.Exists(AppPaths.LogsDirectory)) Directory.Delete(AppPaths.LogsDirectory, true);
            return true;
        }
        catch { return false; }
    }
}
