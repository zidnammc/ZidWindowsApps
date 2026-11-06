using System;
using System.Linq;
using System.Threading;

namespace ZidWindowsApps;

public static class Program
{
    public const string MutexName = @"Local\ZidWindowsApps.SingleInstance";
    public const string ActivateEventName = @"Local\ZidWindowsApps.Activate";

    [STAThread]
    public static int Main(string[] args)
    {
        var settings = AppSettings.Load();
        var logger = new ActivityLogger();

        // Mode terjadwal: tanpa UI dan tanpa single-instance.
        if (args.Any(a => a.Equals("--scheduled-cleanup", StringComparison.OrdinalIgnoreCase)))
        {
            logger.CleanupOldLogs(settings.LogRetentionDays);
            ScheduledCleanupService.RunSafeCleanup(settings, logger);
            return 0;
        }

        using var mutex = new Mutex(true, MutexName, out var isFirst);
        var ownsMutex = isFirst;
        if (!isFirst)
        {
            if (args.Any(a => a.Equals("--elevated", StringComparison.OrdinalIgnoreCase)))
            {
                // Instance lama sedang menutup diri setelah meminta hak Administrator: tunggu sebentar.
                try { ownsMutex = mutex.WaitOne(TimeSpan.FromSeconds(10)); } catch (AbandonedMutexException) { ownsMutex = true; }
            }
            if (!ownsMutex)
            {
                // Sudah ada instance lain: minta instance itu tampil, lalu keluar.
                try { using var evt = EventWaitHandle.OpenExisting(ActivateEventName); evt.Set(); } catch { }
                return 0;
            }
        }

        try
        {
            var app = new App(settings, logger);
            return app.Run();
        }
        finally
        {
            if (ownsMutex) { try { mutex.ReleaseMutex(); } catch { } }
        }
    }
}
