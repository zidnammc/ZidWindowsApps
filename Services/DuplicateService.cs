using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace ZidWindowsApps;

public static class DuplicateService
{
    /// <summary>
    /// Mencari file duplikat. Dikelompokkan per ukuran terlebih dahulu; hanya file dengan ukuran sama yang di-hash
    /// (jauh lebih cepat daripada meng-hash semua file).
    /// </summary>
    public static List<DuplicateGroup> Find(string root, IProgress<string>? progress, CancellationToken ct)
    {
        root = Environment.ExpandEnvironmentVariables(root.Trim().Trim('"'));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Folder tidak ditemukan: {root}");

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
        };

        progress?.Report("Membaca daftar file...");
        var bySize = new Dictionary<long, List<string>>();
        int scanned = 0;
        foreach (var fi in new DirectoryInfo(root).EnumerateFiles("*", options))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (fi.Length == 0) continue;
                if (!bySize.TryGetValue(fi.Length, out var list)) bySize[fi.Length] = list = new List<string>();
                list.Add(fi.FullName);
            }
            catch { }
            if (++scanned % 500 == 0) progress?.Report($"Membaca daftar file... {scanned:N0}");
        }

        var candidates = bySize.Where(kv => kv.Value.Count > 1).ToList();
        int total = candidates.Sum(kv => kv.Value.Count), done = 0;
        var groups = new List<DuplicateGroup>();

        foreach (var (size, files) in candidates)
        {
            var byHash = new Dictionary<string, List<string>>();
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Memeriksa isi file {++done:N0}/{total:N0}");
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20, FileOptions.SequentialScan);
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    if (!byHash.TryGetValue(hash, out var list)) byHash[hash] = list = new List<string>();
                    list.Add(file);
                }
                catch { /* file terkunci / hilang: lewati */ }
            }
            foreach (var (hash, same) in byHash.Where(kv => kv.Value.Count > 1))
                groups.Add(new DuplicateGroup { Hash = hash, Size = size, Files = same });
        }

        return groups.OrderByDescending(g => g.Size * (g.Files.Count - 1)).ToList();
    }
}
