using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ZidWindowsApps;

public static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    /// <summary>Folder Downloads yang sebenarnya (bisa dipindah pengguna), bukan sekadar %USERPROFILE%\Downloads.</summary>
    public static string Downloads
    {
        get
        {
            try
            {
                if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out var ptr) == 0)
                {
                    try { return Marshal.PtrToStringUni(ptr) ?? Fallback(); }
                    finally { Marshal.FreeCoTaskMem(ptr); }
                }
            }
            catch { }
            return Fallback();
        }
    }

    private static string Fallback() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
}
