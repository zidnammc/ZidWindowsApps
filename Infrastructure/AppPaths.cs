using System;
using System.IO;

namespace ZidWindowsApps;

/// <summary>Lokasi file milik aplikasi (satu tempat, tidak tersebar di banyak kelas).</summary>
public static class AppPaths
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string DataDirectory => Path.Combine(Local, "ZidWindowsApps");
    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    public static string RegistryBackupDirectory => Path.Combine(DataDirectory, "RegistryBackups");

    // Lokasi lama dipertahankan supaya pengaturan pengguna yang sudah ada tetap terbaca.
    public static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZidWindowsApps", "settings.json");

    /// <summary>
    /// Perintah untuk menjalankan ulang aplikasi ini. Bila dijalankan lewat 'dotnet ZidWindowsApps.dll',
    /// host-nya dotnet.exe sehingga DLL harus ikut disertakan sebagai argumen.
    /// </summary>
    public static (string File, string Arguments) LaunchCommand(string extraArgs = "")
    {
        var exe = ExecutablePath;
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var dll = Path.Combine(AppContext.BaseDirectory, "ZidWindowsApps.dll");
            return (exe, $"\"{dll}\" {extraArgs}".Trim());
        }
        return (exe, extraArgs);
    }

    public static string ExecutablePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ZidWindowsApps.exe");
}
