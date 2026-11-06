using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace ZidWindowsApps;

public static class InstalledAppsService
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    // Nama uninstaller yang terlalu umum untuk dipakai sebagai kata kunci pencarian leftover.
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "unins000", "unins001", "uninstall", "uninst", "uninstaller", "setup", "update", "msiexec", "installer", "remove", "install",
    };

    private static readonly HashSet<string> IgnoredRegistryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Classes", "Policies", "Windows", "Wow6432Node", "RegisteredApplications", "Clients", "ODBC", "Intel", "Nvidia Corporation",
    };

    // ---------------------------------------------------------------- daftar aplikasi

    public static List<InstalledApp> GetInstalledApps()
    {
        var apps = new List<InstalledApp>();
        ReadUninstallKeys(apps, RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM64");
        ReadUninstallKeys(apps, RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM32");
        ReadUninstallKeys(apps, RegistryHive.CurrentUser, RegistryView.Default, "HKCU");
        apps.AddRange(ReadStoreApps());

        return apps
            .GroupBy(a => $"{a.Name}|{a.Version}|{a.Publisher}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void ReadUninstallKeys(List<InstalledApp> apps, RegistryHive hive, RegistryView view, string tag)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(UninstallPath);
            if (root == null) return;

            foreach (var subName in root.GetSubKeyNames())
            {
                try
                {
                    using var key = root.OpenSubKey(subName);
                    if (key == null) continue;
                    var name = (key.GetValue("DisplayName") as string)?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (Convert.ToInt32(key.GetValue("SystemComponent") ?? 0) == 1) continue;           // komponen sistem
                    if (!string.IsNullOrEmpty(key.GetValue("ParentKeyName") as string)) continue;        // update/patch
                    var release = key.GetValue("ReleaseType") as string;
                    if (release is "Update" or "Hotfix" or "Security Update") continue;

                    var uninstall = (key.GetValue("UninstallString") as string) ?? (key.GetValue("QuietUninstallString") as string) ?? "";
                    if (string.IsNullOrWhiteSpace(uninstall)) continue;

                    apps.Add(new InstalledApp
                    {
                        Name = name,
                        Version = (key.GetValue("DisplayVersion") as string) ?? "",
                        Publisher = (key.GetValue("Publisher") as string) ?? "",
                        InstallLocation = ((key.GetValue("InstallLocation") as string) ?? "").Trim().Trim('"'),
                        UninstallString = uninstall,
                        RegistryView = tag,
                        RegistrySubKey = UninstallPath + "\\" + subName,
                    });
                }
                catch { /* satu entri rusak tidak boleh menggagalkan daftar */ }
            }
        }
        catch { }
    }

    private static IEnumerable<InstalledApp> ReadStoreApps()
    {
        // Tanpa admin, -AllUsers ditolak; kita pakai paket milik pengguna saat ini.
        var allUsers = Admin.IsAdministrator ? "-AllUsers" : "";
        var script = $@"Get-AppxPackage {allUsers} |
Where-Object {{ -not $_.IsFramework -and -not $_.NonRemovable -and -not $_.IsResourcePackage -and $_.SignatureKind -ne 'System' }} |
ForEach-Object {{ [pscustomobject]@{{ Name=$_.Name; Version=$_.Version.ToString(); Publisher=($_.Publisher -replace '^CN=([^,]+).*','$1'); Full=$_.PackageFullName; Location=$_.InstallLocation }} }} |
ConvertTo-Json -Compress";

        var list = new List<InstalledApp>();
        try
        {
            var result = PowerShellService.Run(script, 90);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return list;

            using var doc = JsonDocument.Parse(result.Output);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            foreach (var e in elements)
            {
                string Get(string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                var name = Get("Name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                list.Add(new InstalledApp
                {
                    Name = name,
                    Version = Get("Version"),
                    Publisher = Get("Publisher"),
                    InstallLocation = Get("Location"),
                    PackageFullName = Get("Full"),
                    UninstallString = "(Microsoft Store)",
                    IsStoreApp = true,
                });
            }
        }
        catch { }
        return list;
    }

    // ---------------------------------------------------------------- uninstall

    private static (string File, string Args) SplitCommandLine(string command)
    {
        command = Environment.ExpandEnvironmentVariables(command.Trim());
        string file, args;

        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 0) { file = command[1..end]; args = command[(end + 1)..].Trim(); }
            else { file = command.Trim('"'); args = ""; }
        }
        else
        {
            // Path tanpa tanda kutip yang mengandung spasi (C:\Program Files\App\unins000.exe /S):
            // coba prefix yang makin panjang sampai ketemu file yang benar-benar ada.
            file = ""; args = "";
            for (int i = 0; i < command.Length; i++)
            {
                if (command[i] != ' ' && i != command.Length - 1) continue;
                var candidate = (i == command.Length - 1 ? command : command[..i]).Trim();
                if (File.Exists(candidate)) { file = candidate; args = command[candidate.Length..].Trim(); break; }
                if (File.Exists(candidate + ".exe")) { file = candidate + ".exe"; args = command[candidate.Length..].Trim(); break; }
            }
            if (file.Length == 0)
            {
                var space = command.IndexOf(' ');
                file = space < 0 ? command : command[..space];
                args = space < 0 ? "" : command[(space + 1)..].Trim();
            }
        }

        // "MsiExec /I{GUID}" membuka menu modify/repair; untuk menghapus harus /X.
        if (Path.GetFileNameWithoutExtension(file).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
            args = Regex.Replace(args, @"(?<=^|\s)[/-]I(?=\s*\{)", "/X", RegexOptions.IgnoreCase);

        return (file, args);
    }

    private static Process? StartUninstallProcess(InstalledApp app)
    {
        var (file, args) = SplitCommandLine(app.UninstallString);
        if (string.IsNullOrWhiteSpace(file)) throw new InvalidOperationException("Perintah uninstall tidak ditemukan.");

        var rooted = Path.IsPathRooted(file);
        if (rooted && !File.Exists(file)) throw new FileNotFoundException($"Uninstaller tidak ditemukan: {file}");

        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = true,
            Verb = Admin.IsAdministrator ? "" : "runas",
            WorkingDirectory = rooted ? (Path.GetDirectoryName(file) ?? "") : "",
        };
        return Process.Start(psi);
    }

    public static bool IsStillInstalled(InstalledApp app)
    {
        if (app.IsStoreApp) return false;
        try
        {
            var (hive, view) = app.RegistryView switch
            {
                "HKCU" => (RegistryHive.CurrentUser, RegistryView.Default),
                "HKLM32" => (RegistryHive.LocalMachine, RegistryView.Registry32),
                _ => (RegistryHive.LocalMachine, RegistryView.Registry64),
            };
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(app.RegistrySubKey);
            return key != null;
        }
        catch { return true; }
    }

    private static void CloseRelatedProcesses(InstalledApp app, int waitSeconds)
    {
        // Hanya proses yang benar-benar berjalan dari folder instalasi (bukan sekadar nama yang mirip).
        if (string.IsNullOrWhiteSpace(app.InstallLocation) || PathGuard.IsProtected(app.InstallLocation) || !Directory.Exists(app.InstallLocation)) return;

        var targets = new List<Process>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == Environment.ProcessId) { p.Dispose(); continue; }
                var path = ProcessService.GetImagePath(p.Id);
                if (path.Length > 0 && PathGuard.IsUnder(path, app.InstallLocation)) { targets.Add(p); continue; }
            }
            catch { }
            p.Dispose();
        }

        try
        {
            foreach (var p in targets) { try { p.CloseMainWindow(); } catch { } }
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, waitSeconds));
            while (DateTime.UtcNow < deadline && targets.Any(p => { try { return !p.HasExited; } catch { return false; } }))
                Thread.Sleep(300);
        }
        finally { foreach (var p in targets) p.Dispose(); }
    }

    /// <summary>Menjalankan uninstaller dan memverifikasi hasilnya. Panggil dari background thread.</summary>
    public static UninstallOutcome UninstallFully(InstalledApp app, int waitSeconds)
    {
        if (app.IsStoreApp)
        {
            var allUsers = Admin.IsAdministrator ? " -AllUsers" : "";
            var r = PowerShellService.Run($"$ErrorActionPreference='Stop'; Remove-AppxPackage -Package {PowerShellService.Quote(app.PackageFullName)}{allUsers}", 300);
            return r.Success
                ? new UninstallOutcome(true, true, "Aplikasi Store dihapus.")
                : new UninstallOutcome(true, false, string.IsNullOrWhiteSpace(r.Combined) ? "Gagal menghapus aplikasi Store." : r.Combined);
        }

        CloseRelatedProcesses(app, waitSeconds);

        try
        {
            using var process = StartUninstallProcess(app);
            process?.WaitForExit(5 * 60 * 1000);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new UninstallOutcome(false, false, "Permintaan izin Administrator dibatalkan.");
        }

        // Banyak uninstaller meluncurkan proses lain lalu langsung selesai: tunggu sampai entri registry benar-benar hilang.
        for (int i = 0; i < 45; i++)
        {
            if (!IsStillInstalled(app)) return new UninstallOutcome(true, true, "Aplikasi sudah terhapus.");
            Thread.Sleep(2000);
        }
        return new UninstallOutcome(true, false, "Aplikasi masih terdaftar. Uninstall mungkin dibatalkan atau belum selesai.");
    }

    // ---------------------------------------------------------------- leftover

    public static List<LeftoverItem> FindLeftovers(InstalledApp app, IEnumerable<string> excludedPaths)
    {
        var excluded = excludedPaths.ToList();
        var names = new List<string> { app.Name.Trim() };
        if (!app.IsStoreApp)
        {
            var stem = Path.GetFileNameWithoutExtension(SplitCommandLine(app.UninstallString).File);
            if (!string.IsNullOrWhiteSpace(stem) && !GenericNames.Contains(stem)) names.Add(stem);
        }
        names = names.Where(n => n.Length >= 4).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<LeftoverItem>();
        if (names.Count == 0) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFolder(string path)
        {
            if (!seen.Add(PathGuard.Normalize(path))) return;
            if (PathGuard.IsProtected(path) || PathGuard.IsExcluded(path, excluded)) return;
            result.Add(new LeftoverItem { Kind = LeftoverKind.Folder, Target = path });
        }

        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
            AddFolder(app.InstallLocation);

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        }.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root).Take(5000))
                {
                    var folderName = Path.GetFileName(dir);
                    if (names.Any(n => folderName.Contains(n, StringComparison.OrdinalIgnoreCase))) AddFolder(dir);
                    if (result.Count >= 40) break;
                }
            }
            catch { }
        }

        AddRegistryLeftovers(result, names, RegistryHive.CurrentUser, @"Software", false);
        AddRegistryLeftovers(result, names, RegistryHive.LocalMachine, @"SOFTWARE", true);
        AddRegistryLeftovers(result, names, RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node", true);
        return result;
    }

    private static void AddRegistryLeftovers(List<LeftoverItem> result, List<string> names, RegistryHive hive, string parentPath, bool hklm)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var parent = baseKey.OpenSubKey(parentPath);
            if (parent == null) return;
            foreach (var sub in parent.GetSubKeyNames().Take(3000))
            {
                if (IgnoredRegistryKeys.Contains(sub)) continue;
                if (!names.Any(n => sub.Contains(n, StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(new LeftoverItem { Kind = LeftoverKind.Registry, Target = parentPath + "\\" + sub, IsHklm = hklm });
            }
        }
        catch { }
    }

    /// <summary>Menghapus satu leftover. File/folder -> Recycle Bin; registry -> dicadangkan ke .reg lalu dihapus.</summary>
    public static bool TryRemoveLeftover(LeftoverItem item, out string error)
    {
        error = "";
        try
        {
            if (item.Kind == LeftoverKind.Registry)
                return RemoveRegistryKey(item, out error);

            if (PathGuard.IsProtected(item.Target))
            {
                error = "Folder ini dilindungi dan tidak boleh dihapus.";
                return false;
            }
            if (!File.Exists(item.Target) && !Directory.Exists(item.Target)) return true;
            var ok = RecycleBinService.TrySend(item.Target, out var err);
            error = err ?? "";
            return ok;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool RemoveRegistryKey(LeftoverItem item, out string error)
    {
        error = "";
        var segments = item.Target.Split('\\');
        if (segments.Length < 2 || !segments[0].Equals("Software", StringComparison.OrdinalIgnoreCase) && !segments[0].Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase))
        {
            error = "Key registry di luar HKCU/HKLM\\Software tidak diizinkan.";
            return false;
        }

        // 1) cadangkan
        Directory.CreateDirectory(AppPaths.RegistryBackupDirectory);
        var safe = Regex.Replace(item.Target, @"[^\w\-]+", "_");
        var backup = Path.Combine(AppPaths.RegistryBackupDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{(item.IsHklm ? "HKLM" : "HKCU")}-{safe}.reg");
        var psi = new ProcessStartInfo("reg.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var a in new[] { "export", $"{(item.IsHklm ? "HKLM" : "HKCU")}\\{item.Target}", backup, "/y", "/reg:64" }) psi.ArgumentList.Add(a);
        using (var p = Process.Start(psi))
        {
            p!.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            if (p.ExitCode != 0 || !File.Exists(backup))
            {
                error = "Cadangan registry gagal dibuat, key tidak dihapus.";
                return false;
            }
        }

        // 2) hapus
        using var baseKey = RegistryKey.OpenBaseKey(item.IsHklm ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);
        var parentPath = string.Join('\\', segments[..^1]);
        using var parent = baseKey.OpenSubKey(parentPath, writable: true);
        if (parent == null) return true;
        try { parent.DeleteSubKeyTree(segments[^1], throwOnMissingSubKey: false); return true; }
        catch (UnauthorizedAccessException) { error = "Butuh hak Administrator untuk menghapus key ini."; return false; }
    }
}
