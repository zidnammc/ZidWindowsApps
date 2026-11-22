using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ZidWindowsApps;

public static class RecycleBinService
{
    // SHQUERYRBINFO memakai packing bawaan di x64/ARM64 (cbSize = 24). Pack = 1 menghasilkan 20 byte
    // sehingga SHQueryRecycleBin selalu gagal dengan E_INVALIDARG.
    [StructLayout(LayoutKind.Sequential)]
    private struct BinInfo { public int Size; public long Bytes; public long Items; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string root, ref BinInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr window, string root, uint flags);

    public static (long Items, long Bytes) ReadSize(string drive)
    {
        var info = new BinInfo { Size = Marshal.SizeOf<BinInfo>() };
        Marshal.ThrowExceptionForHR(SHQueryRecycleBin(drive, ref info));
        return (info.Items, info.Bytes);
    }

    private const uint SHERB_NOCONFIRMATION = 0x1, SHERB_NOPROGRESSUI = 0x2, SHERB_NOSOUND = 0x4;
    private const int E_UNEXPECTED = unchecked((int)0x8000FFFF);

    public static void Empty(string drive)
    {
        // A separate, in-app confirmation names this specific volume and its size.
        int hr = SHEmptyRecycleBin(IntPtr.Zero, drive, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        // Windows mengembalikan E_UNEXPECTED bila Recycle Bin sudah kosong.
        if (hr != E_UNEXPECTED) Marshal.ThrowExceptionForHR(hr);
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;
    // Tampilkan peringatan bila sebuah file TIDAK bisa masuk Recycle Bin (dan akan terhapus permanen),
    // alih-alih diam-diam menghapusnya karena FOF_NOCONFIRMATION.
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    private const ushort Flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOF_WANTNUKEWARNING;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    /// <summary>Memindahkan banyak file/folder ke Recycle Bin dalam satu operasi. True hanya bila semuanya sukses.</summary>
    public static bool TrySend(IReadOnlyList<string> paths, out string? error)
    {
        error = null;
        if (paths.Count == 0) return true;
        if (!Environment.Is64BitProcess)
        {
            error = "Hanya proses 64-bit yang didukung.";
            return false;
        }

        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // Daftar path dipisah \0; marshaller menambahkan \0 penutup terakhir => format double-null.
            pFrom = string.Join("\0", paths) + "\0",
            fFlags = Flags,
        };

        int code;
        try { code = SHFileOperation(ref op); }
        catch (Exception ex) { error = ex.Message; return false; }

        if (code != 0) { error = $"SHFileOperation gagal (kode 0x{code:X})."; return false; }
        if (op.fAnyOperationsAborted) { error = "Operasi dibatalkan oleh Windows."; return false; }
        return true;
    }

    public static bool TrySend(string path, out string? error) => TrySend(new[] { path }, out error);
}
