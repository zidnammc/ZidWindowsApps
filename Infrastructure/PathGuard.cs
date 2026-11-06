using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZidWindowsApps;

/// <summary>
/// Pagar pengaman untuk operasi hapus. Menolak folder "wadah" (drive root, Program Files, Users, AppData, dst.)
/// dan apa pun yang berada di dalam folder Windows.
/// </summary>
public static class PathGuard
{
    private static readonly StringComparison Cmp = StringComparison.OrdinalIgnoreCase;

    public static string Normalize(string path)
    {
        try
        {
            var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            var root = Path.GetPathRoot(full) ?? "";
            return full.Length > root.Length ? full.TrimEnd('\\', '/') : full;
        }
        catch
        {
            return path.Trim();
        }
    }

    private static IEnumerable<string> ContainerFolders()
    {
        var list = new List<string?>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
        };
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            list.Add(Path.GetDirectoryName(profile)); // C:\Users
            list.Add(Path.Combine(profile, "Downloads"));
            list.Add(Path.Combine(profile, "AppData"));
        }
        return list.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Normalize(p!)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsUnder(string path, string parent)
    {
        var p = Normalize(path);
        var d = Normalize(parent);
        if (p.Equals(d, Cmp)) return true;
        return p.StartsWith(d.TrimEnd('\\') + "\\", Cmp);
    }

    /// <summary>True bila path tidak boleh dihapus sebagai "leftover" / target pembersihan.</summary>
    public static bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        var p = Normalize(path);
        var root = Path.GetPathRoot(p);
        if (!string.IsNullOrEmpty(root) && (p.Equals(root.TrimEnd('\\'), Cmp) || p.Equals(root, Cmp))) return true;

        var windows = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (IsUnder(p, windows)) return true;

        foreach (var container in ContainerFolders())
        {
            // sama persis, atau p adalah induk dari folder wadah (mis. C:\Program Files\..)
            if (p.Equals(container, Cmp)) return true;
            if (container.StartsWith(p + "\\", Cmp)) return true;
        }
        return false;
    }

    /// <summary>Cocok dengan daftar pengecualian pengguna, dengan batas folder yang benar.</summary>
    public static bool IsExcluded(string path, IEnumerable<string> excluded)
    {
        foreach (var e in excluded)
        {
            if (string.IsNullOrWhiteSpace(e)) continue;
            if (IsUnder(path, e)) return true;
        }
        return false;
    }
}
