using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ZidWindowsApps;

public static class ProcessService
{
    private static readonly HashSet<string> SystemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "Memory Compression", "Secure System", "Idle", "System Idle Process",
    };

    // Menghentikan proses ini membuat Windows crash / logout paksa. Selalu ditolak.
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsm",
        "svchost", "dwm", "fontdrvhost", "Memory Compression", "Secure System",
    };

    private static readonly ConcurrentDictionary<string, string> PublisherCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CpuGate = new();
    private static Dictionary<(int Id, long Start), (TimeSpan Cpu, DateTime At)> _cpuSamples = new();

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, int flags, StringBuilder text, ref int size);

    /// <summary>Jauh lebih andal daripada Process.MainModule (tidak gagal pada proses 32/64-bit lain atau tanpa hak penuh).</summary>
    public static string GetImagePath(int pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageNameW(handle, 0, sb, ref size) ? sb.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }

    private static string GetPublisher(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return PublisherCache.GetOrAdd(path, p =>
        {
            try { return FileVersionInfo.GetVersionInfo(p).CompanyName ?? ""; }
            catch { return ""; }
        });
    }

    public static List<ProcessInfo> GetProcesses(bool includeSystem)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var now = DateTime.UtcNow;
        var cpuCores = Math.Max(1, Environment.ProcessorCount);
        var result = new List<ProcessInfo>();
        var newSamples = new Dictionary<(int, long), (TimeSpan, DateTime)>();
        Dictionary<(int Id, long Start), (TimeSpan Cpu, DateTime At)> previous;
        lock (CpuGate) previous = _cpuSamples;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == 0) continue;
                    var name = process.ProcessName;
                    var path = GetImagePath(process.Id);

                    DateTime? start = null;
                    try { start = process.StartTime; } catch { }

                    double cpu = 0;
                    try
                    {
                        var total = process.TotalProcessorTime;
                        var key = (process.Id, start?.Ticks ?? 0L);
                        newSamples[key] = (total, now);
                        if (previous.TryGetValue(key, out var old))
                        {
                            var wall = (now - old.At).TotalSeconds;
                            if (wall > 0.2)
                                cpu = Math.Clamp((total - old.Cpu).TotalSeconds / (wall * cpuCores) * 100.0, 0, 100);
                        }
                    }
                    catch { }

                    bool isSystem = SystemNames.Contains(name) ||
                                    (path.Length > 0 && PathGuard.IsUnder(path, windows));
                    bool isProtected = path.Length == 0 && !isSystem;
                    if (!includeSystem && (isSystem || isProtected)) continue;

                    result.Add(new ProcessInfo
                    {
                        Id = process.Id,
                        Name = name,
                        MemoryBytes = process.WorkingSet64,
                        Cpu = cpu,
                        Path = path,
                        Publisher = GetPublisher(path),
                        StartTime = start,
                        IsSystem = isSystem,
                    });
                }
                catch { /* proses berakhir saat dibaca */ }
            }
        }

        lock (CpuGate) _cpuSamples = newSamples!; // sampel proses yang sudah mati ikut terbuang
        return result.OrderByDescending(p => p.MemoryBytes).ToList();
    }

    public static bool IsCritical(ProcessInfo info) => CriticalNames.Contains(info.Name);

    /// <summary>
    /// tree=false: hanya proses itu (coba tutup dengan sopan dulu). tree=true: beserta seluruh proses anaknya.
    /// Melempar InvalidOperationException/TimeoutException dengan pesan yang bisa ditampilkan.
    /// </summary>
    public static void Kill(ProcessInfo info, bool tree)
    {
        if (IsCritical(info))
            throw new InvalidOperationException($"'{info.Name}' adalah proses inti Windows. Menghentikannya dapat membuat Windows crash, jadi ditolak.");
        if (info.Id == Environment.ProcessId)
            throw new InvalidOperationException("Aplikasi ini tidak bisa menghentikan dirinya sendiri dari daftar proses.");

        Process process;
        try { process = Process.GetProcessById(info.Id); }
        catch (ArgumentException) { return; } // sudah berhenti

        using (process)
        {
            try
            {
                if (!process.ProcessName.Equals(info.Name, StringComparison.OrdinalIgnoreCase) ||
                    (info.StartTime.HasValue && Math.Abs((process.StartTime - info.StartTime.Value).TotalSeconds) > 2))
                    throw new InvalidOperationException("PID ini sudah dipakai proses lain. Refresh daftar lalu coba lagi.");

                if (process.HasExited) return;

                if (!tree && process.CloseMainWindow() && process.WaitForExit(3000)) return;

                process.Kill(entireProcessTree: tree); // sebelumnya: selalu true, sehingga "hentikan proses" ikut mematikan anak-anaknya
                if (!process.WaitForExit(5000))
                    throw new TimeoutException($"'{info.Name}' belum berhenti setelah 5 detik.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("exited", StringComparison.OrdinalIgnoreCase) ||
                                                       ex.Message.Contains("sudah berhenti", StringComparison.OrdinalIgnoreCase)) { }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException($"Akses ditolak saat menghentikan '{info.Name}'. Coba jalankan aplikasi sebagai Administrator. ({ex.Message})");
            }
        }
    }
}
