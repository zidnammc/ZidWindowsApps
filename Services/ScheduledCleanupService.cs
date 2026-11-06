using System;
using System.Diagnostics;

namespace ZidWindowsApps;

public static class ScheduledCleanupService
{
    private const string TaskName = @"ZidWindowsApps\Scheduled Safe Cleanup";

    private static (int Code, string Text) Schtasks(params string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        // Baca stdout & stderr bersamaan agar tidak deadlock.
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(30000)) { try { p.Kill(true); } catch { } return (-1, "schtasks tidak merespons."); }
        return (p.ExitCode, (stdout.Result + " " + stderr.Result).Trim());
    }

    private static string TaskCommand()
    {
        var (file, args) = AppPaths.LaunchCommand("--scheduled-cleanup");
        return $"\"{file}\" {args}".Trim();
    }

    private static string Modifier(string frequency) => frequency.Trim().ToLowerInvariant() switch
    {
        "harian" or "daily" => "DAILY",
        "bulanan" or "monthly" => "MONTHLY",
        _ => "WEEKLY",
    };

    /// <summary>Membuat/menghapus tugas terjadwal. Mengembalikan hasil nyata (sebelumnya selalu dianggap sukses).</summary>
    public static (bool Ok, string Message) Configure(bool enabled, string frequency)
    {
        try
        {
            var exists = Schtasks("/Query", "/TN", TaskName).Code == 0;
            if (!enabled)
            {
                if (!exists) return (true, "Jadwal pembersihan nonaktif.");
                var del = Schtasks("/Delete", "/TN", TaskName, "/F");
                return del.Code == 0 ? (true, "Jadwal pembersihan dihapus.") : (false, "Gagal menghapus jadwal: " + del.Text);
            }

            var args = new System.Collections.Generic.List<string>
            {
                "/Create", "/F", "/TN", TaskName,
                "/TR", TaskCommand(),
                "/SC", Modifier(frequency), "/ST", "03:00",
            };
            if (Admin.IsAdministrator) { args.Add("/RL"); args.Add("HIGHEST"); }

            var r = Schtasks(args.ToArray());
            return r.Code == 0
                ? (true, $"Jadwal pembersihan aktif ({frequency}, 03:00).")
                : (false, "Gagal membuat jadwal: " + r.Text);
        }
        catch (Exception ex)
        {
            return (false, "Gagal mengatur jadwal: " + ex.Message);
        }
    }

    /// <summary>Dijalankan oleh Task Scheduler (--scheduled-cleanup). Hanya kategori aman; tanpa browser cache & Downloads.</summary>
    public static void RunSafeCleanup(AppSettings settings, ActivityLogger logger)
    {
        try
        {
            var service = new CleanupService(settings);
            var items = service.Scan(includeBrowserCache: false, includeDownloads: false);
            var result = service.Delete(items, scheduled: true, progress: null);
            logger.Write($"Pembersihan terjadwal: {result.Deleted} dipindahkan ke Recycle Bin ({Format.Bytes(result.FreedBytes)}), {result.Failed} gagal, {result.Skipped} dilewati.");
        }
        catch (Exception ex)
        {
            logger.Write("Pembersihan terjadwal gagal: " + ex.Message);
        }
    }
}
