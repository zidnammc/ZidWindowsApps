using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ZidWindowsApps;

public sealed record CleanupResult(int Deleted, int Failed, int Skipped, long FreedBytes);

public sealed class CleanupService
{
    private const long ScheduledMaxFileBytes = 100L * 1024 * 1024;

    private readonly AppSettings _settings;

    public CleanupService(AppSettings settings) => _settings = settings;

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string WindowsDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    /// <summary>
    /// Mencari kandidat pembersihan. Satu baris per kategori (kecuali Downloads: satu baris per file
    /// supaya pengguna bisa memilih satu per satu).
    /// </summary>
    public List<CleanupItem> Scan(bool includeBrowserCache, bool includeDownloads, CancellationToken ct = default)
    {
        var items = new List<CleanupItem>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Category(string name, string root, bool recursive, int minAgeDays, string pattern = "*")
        {
            if (!seenRoots.Add(PathGuard.Normalize(root) + "|" + pattern)) return; // hindari kategori ganda
            var files = Collect(root, recursive, minAgeDays, pattern, ct, out long size);
            if (files.Count > 0)
                items.Add(new CleanupItem { Category = name, Path = root, Size = size, Files = files });
        }

        // Temp pengguna: dulu "Temporary Windows" & "Temporary User" menunjuk ke folder yang sama (terhitung ganda).
        Category("File sementara pengguna", Path.GetTempPath(), true, 1);
        Category("File sementara pengguna (AppData)", Path.Combine(LocalAppData, "Temp"), true, 1);
        Category("File sementara Windows", Path.Combine(WindowsDir, "Temp"), true, 1);

        Category("Cache thumbnail Explorer", Path.Combine(LocalAppData, "Microsoft", "Windows", "Explorer"), false, 0, "thumbcache_*.db");
        Category("Laporan error Windows (pengguna)", Path.Combine(LocalAppData, "Microsoft", "Windows", "WER"), true, 1);
        Category("Laporan error Windows (sistem)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER"), true, 1);
        Category("Crash dump aplikasi", Path.Combine(LocalAppData, "CrashDumps"), true, 0);
        Category("Cache Windows Update", Path.Combine(WindowsDir, "SoftwareDistribution", "Download"), true, 0);
        Category("Cache Delivery Optimization", Path.Combine(WindowsDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"), true, 0);
        Category("Riwayat file terbaru (Recent)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Recent"), false, 0, "*.lnk");

        if (includeBrowserCache)
        {
            AddBrowser(items, "Cache Microsoft Edge", Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"), ct);
            AddBrowser(items, "Cache Google Chrome", Path.Combine(LocalAppData, "Google", "Chrome", "User Data"), ct);
        }

        if (includeDownloads)
        {
            var downloads = KnownFolders.Downloads;
            foreach (var file in Collect(downloads, false, 30, "*", ct, out _))
            {
                long len = SafeLength(file);
                items.Add(new CleanupItem
                {
                    Category = "Downloads > 30 hari (periksa manual)",
                    Path = file,
                    Size = len,
                    Files = new List<string> { file },
                    Selected = false, // jangan pernah terpilih otomatis
                });
            }
        }

        return items;
    }

    private void AddBrowser(List<CleanupItem> items, string name, string userData, CancellationToken ct)
    {
        if (!Directory.Exists(userData)) return;
        var files = new List<string>();
        long total = 0;
        string[] cacheDirs = { "Cache", "Code Cache", "GPUCache" };

        IEnumerable<string> profiles;
        try
        {
            profiles = Directory.EnumerateDirectories(userData)
                .Where(d => { var n = Path.GetFileName(d); return n == "Default" || n.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase); })
                .ToList();
        }
        catch { return; }

        foreach (var profile in profiles)
            foreach (var cache in cacheDirs)
            {
                var dir = Path.Combine(profile, cache);
                if (!Directory.Exists(dir)) continue;
                files.AddRange(Collect(dir, true, 0, "*", ct, out long size));
                total += size;
            }

        if (files.Count > 0)
            items.Add(new CleanupItem { Category = name, Path = userData, Size = total, Files = files });
    }

    private List<string> Collect(string root, bool recursive, int minAgeDays, string pattern, CancellationToken ct, out long totalSize)
    {
        totalSize = 0;
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;
        if (PathGuard.IsExcluded(root, _settings.ExcludedPaths)) return result;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,          // satu folder terkunci tidak menggagalkan seluruh scan
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            ReturnSpecialDirectories = false,
        };
        var limit = DateTime.UtcNow.AddDays(-minAgeDays);

        try
        {
            foreach (var fi in new DirectoryInfo(root).EnumerateFiles(pattern, options))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (fi.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    if (minAgeDays > 0 && Math.Max(fi.LastWriteTimeUtc.Ticks, fi.CreationTimeUtc.Ticks) > limit.Ticks) continue;
                    if (PathGuard.IsExcluded(fi.FullName, _settings.ExcludedPaths)) continue;
                    result.Add(fi.FullName);
                    totalSize += fi.Length;
                }
                catch { /* file hilang saat dipindai */ }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* folder bermasalah: lewati */ }
        return result;
    }

    private static long SafeLength(string file)
    {
        try { return new FileInfo(file).Length; } catch { return 0; }
    }

    /// <summary>Memindahkan file ke Recycle Bin. Berjalan di background thread (jangan dipanggil dari UI thread).</summary>
    public CleanupResult Delete(IEnumerable<CleanupItem> items, bool scheduled, IProgress<string>? progress, CancellationToken ct = default)
    {
        int deleted = 0, failed = 0, skipped = 0;
        long freed = 0;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Membersihkan: {item.Category}");

            var batch = new List<(string Path, long Size)>();
            foreach (var file in item.Files)
            {
                if (!File.Exists(file)) { skipped++; continue; }
                if (PathGuard.IsExcluded(file, _settings.ExcludedPaths)) { skipped++; continue; }
                long len = SafeLength(file);
                // Mode terjadwal berjalan tanpa pengawasan: lewati file besar agar tidak memicu dialog Windows.
                if (scheduled && len > ScheduledMaxFileBytes) { skipped++; continue; }
                batch.Add((file, len));
            }

            foreach (var chunk in batch.Chunk(100))
            {
                ct.ThrowIfCancellationRequested();
                var paths = chunk.Select(c => c.Path).ToList();
                if (RecycleBinService.TrySend(paths, out _))
                {
                    deleted += chunk.Length;
                    freed += chunk.Sum(c => c.Size);
                    continue;
                }
                // Salah satu file gagal (terkunci, dll.): coba satu per satu agar sisanya tetap terhapus.
                foreach (var (path, size) in chunk)
                {
                    if (!File.Exists(path)) { deleted++; continue; }
                    if (RecycleBinService.TrySend(path, out _)) { deleted++; freed += size; }
                    else failed++;
                }
            }
        }
        return new CleanupResult(deleted, failed, skipped, freed);
    }
}
