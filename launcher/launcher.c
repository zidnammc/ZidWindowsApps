/*
 * ZidWindowsApps launcher (pengganti "apphost" .NET untuk build lintas-platform).
 * Mencari hostfxr.dll dari .NET Runtime yang terpasang lalu menjalankan ZidWindowsApps.dll di proses yang sama.
 * Subsystem GUI: tidak ada jendela konsol; bila runtime tidak ada, hostfxr menampilkan dialog unduhan resmi .NET.
 */
#include <windows.h>
#include <shellapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <wchar.h>

typedef int (__cdecl *hostfxr_main_startupinfo_fn)(const int argc, const wchar_t *argv[],
        const wchar_t *host_path, const wchar_t *dotnet_root, const wchar_t *app_path);

static int is_dir(const wchar_t *p) { DWORD a = GetFileAttributesW(p); return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY); }
static int is_file(const wchar_t *p) { DWORD a = GetFileAttributesW(p); return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY); }

static int find_dotnet_root(wchar_t *out, size_t cap)
{
    wchar_t tmp[MAX_PATH], fxr[MAX_PATH];
    DWORD n = GetEnvironmentVariableW(L"DOTNET_ROOT", tmp, MAX_PATH);
    if (n > 0 && n < MAX_PATH) {
        swprintf(fxr, MAX_PATH, L"%ls\\host\\fxr", tmp);
        if (is_dir(fxr)) { wcsncpy(out, tmp, cap); return 1; }
    }
    HKEY key;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64", 0, KEY_READ | KEY_WOW64_64KEY, &key) == ERROR_SUCCESS) {
        DWORD size = sizeof(tmp), type = 0;
        if (RegQueryValueExW(key, L"InstallLocation", NULL, &type, (LPBYTE)tmp, &size) == ERROR_SUCCESS && type == REG_SZ) {
            swprintf(fxr, MAX_PATH, L"%ls\\host\\fxr", tmp);
            if (is_dir(fxr)) { wcsncpy(out, tmp, cap); RegCloseKey(key); return 1; }
        }
        RegCloseKey(key);
    }
    n = GetEnvironmentVariableW(L"ProgramW6432", tmp, MAX_PATH);
    if (n == 0) n = GetEnvironmentVariableW(L"ProgramFiles", tmp, MAX_PATH);
    if (n > 0 && n < MAX_PATH - 8) {
        wcscat(tmp, L"\\dotnet");
        swprintf(fxr, MAX_PATH, L"%ls\\host\\fxr", tmp);
        if (is_dir(fxr)) { wcsncpy(out, tmp, cap); return 1; }
    }
    return 0;
}

/* Pilih folder versi hostfxr tertinggi yang berisi hostfxr.dll. */
static int find_hostfxr(const wchar_t *root, wchar_t *out, size_t cap)
{
    wchar_t pattern[MAX_PATH], best[MAX_PATH] = L"";
    int bmaj = -1, bmin = -1, bpat = -1;
    swprintf(pattern, MAX_PATH, L"%ls\\host\\fxr\\*", root);
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE) return 0;
    do {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) || fd.cFileName[0] == L'.') continue;
        int maj = 0, min = 0, pat = 0;
        if (swscanf(fd.cFileName, L"%d.%d.%d", &maj, &min, &pat) < 1) continue;
        wchar_t cand[MAX_PATH];
        swprintf(cand, MAX_PATH, L"%ls\\host\\fxr\\%ls\\hostfxr.dll", root, fd.cFileName);
        if (!is_file(cand)) continue;
        if (maj > bmaj || (maj == bmaj && (min > bmin || (min == bmin && pat > bpat)))) {
            bmaj = maj; bmin = min; bpat = pat;
            wcsncpy(best, cand, MAX_PATH - 1);
        }
    } while (FindNextFileW(h, &fd));
    FindClose(h);
    if (bmaj < 0) return 0;
    wcsncpy(out, best, cap);
    return 1;
}

static void fail(const wchar_t *msg)
{
    int r = MessageBoxW(NULL, msg, L"ZidWindowsApps", MB_YESNO | MB_ICONERROR);
    if (r == IDYES) ShellExecuteW(NULL, L"open", L"https://dotnet.microsoft.com/download/dotnet", NULL, NULL, SW_SHOWNORMAL);
}

int WINAPI wWinMain(HINSTANCE inst, HINSTANCE prev, PWSTR cmd, int show)
{
    (void)inst; (void)prev; (void)cmd; (void)show;
    wchar_t exe[MAX_PATH], dir[MAX_PATH], app[MAX_PATH], root[MAX_PATH], fxr[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    wcsncpy(dir, exe, MAX_PATH);
    wchar_t *slash = wcsrchr(dir, L'\\');
    if (slash) *slash = 0;
    swprintf(app, MAX_PATH, L"%ls\\ZidWindowsApps.dll", dir);

    if (!is_file(app)) { MessageBoxW(NULL, L"ZidWindowsApps.dll tidak ditemukan di folder aplikasi. Instal ulang aplikasi.", L"ZidWindowsApps", MB_OK | MB_ICONERROR); return 2; }

    if (!find_dotnet_root(root, MAX_PATH) || !find_hostfxr(root, fxr, MAX_PATH)) {
        fail(L"ZidWindowsApps membutuhkan .NET Desktop Runtime 8 (atau lebih baru), x64.\n\nBuka halaman unduhan sekarang?");
        return 3;
    }

    HMODULE lib = LoadLibraryW(fxr);
    if (!lib) { fail(L"hostfxr.dll tidak dapat dimuat. Instal ulang .NET Desktop Runtime.\n\nBuka halaman unduhan sekarang?"); return 4; }
    hostfxr_main_startupinfo_fn fn = (hostfxr_main_startupinfo_fn)GetProcAddress(lib, "hostfxr_main_startupinfo");
    if (!fn) { fail(L".NET Runtime terlalu lama/rusak (hostfxr_main_startupinfo tidak ada).\n\nBuka halaman unduhan sekarang?"); return 5; }

    int argc = 0;
    LPWSTR *argvw = CommandLineToArgvW(GetCommandLineW(), &argc);
    const wchar_t **argv = (const wchar_t **)calloc((size_t)argc + 1, sizeof(wchar_t *));
    if (!argv || !argvw) return 6;
    argv[0] = exe;
    for (int i = 1; i < argc; i++) argv[i] = argvw[i];

    return fn(argc, argv, exe, root, app);
}
