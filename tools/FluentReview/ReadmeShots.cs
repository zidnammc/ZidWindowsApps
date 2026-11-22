using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ZidWindowsApps;

// Tangkapan layar README: LiveWindow asli dengan data contoh anonim (tanpa data PC yang menjalankan alat ini).
static partial class Review
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    const string DemoUser = "Demo", DemoMachine = "DESKTOP-DEMO", DemoWindows = "Windows 11 Pro 24H2 (build 26100.2454, 64-bit)";
    static readonly string RealWindows = (string)typeof(SystemInfoService).GetMethod("WindowsVersionText", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;

    static void Set(object o, string field, object? value) => o.GetType().GetField(field, Fields)!.SetValue(o, value);

    static List<string> Files(int n) => Enumerable.Range(0, n).Select(i => "f" + i).ToList();

    static ProcessInfo P(int id, string name, double mb, double cpu, string publisher, string path, bool system = false) =>
        new() { Id = id, Name = name, MemoryBytes = (long)(mb * 1024 * 1024), Cpu = cpu, Publisher = publisher, Path = path, IsSystem = system, StartTime = DateTime.Now.AddMinutes(-(id % 90)) };

    static InstalledApp A(string name, string version, string publisher, double mb, string date, string location) =>
        new() { Name = name, Version = version, Publisher = publisher, SizeBytes = mb > 0 ? (long)(mb * 1024 * 1024) : null, InstallDate = date, InstallLocation = location, IsStoreApp = mb == 0 };

    static void Inject(Window w)
    {
        var u = @"C:\Users\" + DemoUser;
        var processes = new List<ProcessInfo>
        {
            P(9132, "msedge", 612.4, 3.1, "Microsoft Corporation", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
            P(7720, "Code", 488.9, 1.4, "Microsoft Corporation", u + @"\AppData\Local\Programs\Microsoft VS Code\Code.exe"),
            P(5408, "chrome", 402.7, 2.2, "Google LLC", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            P(6012, "Spotify", 236.5, 0.6, "Spotify AB", u + @"\AppData\Roaming\Spotify\Spotify.exe"),
            P(4416, "Discord", 214.3, 0.4, "Discord Inc.", u + @"\AppData\Local\Discord\app-1.0.9200\Discord.exe"),
            P(3880, "explorer", 168.0, 0.8, "Microsoft Corporation", @"C:\Windows\explorer.exe", true),
            P(9876, "ZidWindowsApps", 118.4, 0.9, "Zid", @"C:\Program Files\ZidWindowsApps\ZidWindowsApps.exe"),
            P(8204, "OneDrive", 96.2, 0.1, "Microsoft Corporation", u + @"\AppData\Local\Microsoft\OneDrive\OneDrive.exe"),
            P(2964, "ms-teams", 92.8, 0.3, "Microsoft Corporation", @"C:\Program Files\WindowsApps\MSTeams_24295.605.3225.8804_x64__8wekyb3d8bbwe\ms-teams.exe"),
            P(10544, "steamwebhelper", 71.5, 0.2, "Valve Corporation", @"C:\Program Files (x86)\Steam\bin\cef\cef.win7x64\steamwebhelper.exe"),
            P(11020, "notepad++", 24.1, 0.0, "Notepad++ Team", @"C:\Program Files\Notepad++\notepad++.exe"),
            P(12408, "7zFM", 12.6, 0.0, "Igor Pavlov", @"C:\Program Files\7-Zip\7zFM.exe"),
        };
        var startup = new List<StartupItem>
        {
            new() { Name = "OneDrive", Location = "HKCU Run", Command = "\"" + u + "\\AppData\\Local\\Microsoft\\OneDrive\\OneDrive.exe\" /background" },
            new() { Name = "Discord", Location = "HKCU Run", Command = "\"" + u + "\\AppData\\Local\\Discord\\Update.exe\" --processStart Discord.exe", IsEnabled = false },
            new() { Name = "Spotify", Location = "HKCU Run", Command = "\"" + u + "\\AppData\\Roaming\\Spotify\\Spotify.exe\" --autostart --minimized", IsEnabled = false },
            new() { Name = "Steam", Location = "HKCU Run", Command = "\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent" },
            new() { Name = "SecurityHealth", Location = "HKLM Run", Command = "%windir%\\system32\\SecurityHealthSystray.exe", IsMachineWide = true },
            new() { Name = "Microsoft Edge Update", Location = "Layanan", Command = "\"C:\\Program Files (x86)\\Microsoft\\EdgeUpdate\\MicrosoftEdgeUpdate.exe\" /svc", Kind = StartupKind.WindowsService, IsMachineWide = true },
            new() { Name = "Google Updater", Location = "Layanan", Command = "\"C:\\Program Files (x86)\\Google\\GoogleUpdater\\updater.exe\" --system --windows-service", Kind = StartupKind.WindowsService, IsMachineWide = true, IsEnabled = false },
            new() { Name = "Notepad++.lnk", Location = "Folder Startup", Command = @"C:\Program Files\Notepad++\notepad++.exe", Kind = StartupKind.StartupFolder, IsEnabled = false },
        };
        var apps = new List<InstalledApp>
        {
            A("7-Zip 24.08 (x64)", "24.08", "Igor Pavlov", 5.9, "14/08/2026", @"C:\Program Files\7-Zip"),
            A("Discord", "1.0.9200", "Discord Inc.", 412.0, "02/10/2026", u + @"\AppData\Local\Discord"),
            A("Git", "2.47.1", "The Git Development Community", 338.5, "21/09/2026", @"C:\Program Files\Git"),
            A("Google Chrome", "131.0.6778.86", "Google LLC", 612.3, "12/11/2026", @"C:\Program Files\Google\Chrome\Application"),
            A("Microsoft Edge", "131.0.2903.70", "Microsoft Corporation", 701.2, "15/11/2026", @"C:\Program Files (x86)\Microsoft\Edge\Application"),
            A("Microsoft Teams", "24295.605.3225.8804", "Microsoft Corporation", 0, "", @"C:\Program Files\WindowsApps\MSTeams_24295.605.3225.8804_x64__8wekyb3d8bbwe"),
            A("Microsoft Visual Studio Code (User)", "1.95.3", "Microsoft Corporation", 389.1, "08/11/2026", u + @"\AppData\Local\Programs\Microsoft VS Code"),
            A("Notepad++ (64-bit x64)", "8.7.1", "Notepad++ Team", 15.8, "19/10/2026", @"C:\Program Files\Notepad++"),
            A("Spotify", "1.2.50.335", "Spotify AB", 298.4, "03/11/2026", u + @"\AppData\Roaming\Spotify"),
            A("Steam", "2.10.91.91", "Valve Corporation", 1240.0, "30/07/2026", @"C:\Program Files (x86)\Steam"),
            A("VLC media player", "3.0.21", "VideoLAN", 172.6, "11/06/2026", @"C:\Program Files\VideoLAN\VLC"),
            A("Windows Terminal", "1.21.3231.0", "Microsoft Corporation", 0, "", @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.21.3231.0_x64__8wekyb3d8bbwe"),
        };
        var cache = new List<CleanupItem>
        {
            new() { Category = "File sementara pengguna", Path = u + @"\AppData\Local\Temp\", Size = 1_284_000_000, Files = Files(2140) },
            new() { Category = "File sementara Windows", Path = @"C:\Windows\Temp", Size = 312_400_000, Files = Files(186) },
            new() { Category = "Cache thumbnail Explorer", Path = u + @"\AppData\Local\Microsoft\Windows\Explorer", Size = 96_300_000, Files = Files(9) },
            new() { Category = "Laporan error Windows (pengguna)", Path = u + @"\AppData\Local\Microsoft\Windows\WER", Size = 41_800_000, Files = Files(37) },
            new() { Category = "Cache Windows Update", Path = @"C:\Windows\SoftwareDistribution\Download", Size = 845_100_000, Files = Files(64) },
            new() { Category = "Cache Microsoft Edge", Path = u + @"\AppData\Local\Microsoft\Edge\User Data\Default\Cache", Size = 402_600_000, Files = Files(1820) },
            new() { Category = "Cache Google Chrome", Path = u + @"\AppData\Local\Google\Chrome\User Data\Default\Cache", Size = 288_900_000, Files = Files(1355) },
            new() { Category = "Riwayat file terbaru (Recent)", Path = u + @"\AppData\Roaming\Microsoft\Windows\Recent", Size = 1_200_000, Files = Files(142), Selected = false },
        };
        var system = new SystemSnapshot
        {
            OperatingSystem = DemoWindows,
            ComputerName = DemoMachine,
            Processor = "AMD Ryzen 7 7840HS w/ Radeon 780M Graphics (8 core, 16 thread)",
            Memory = "32 GB (terpakai 41%)",
            Graphics = "AMD Radeon 780M Graphics (driver 31.0.24027.1012)",
            Storage = "C: NVMe SSD 953,87 GB (bebas 412,58 GB)",
            Battery = "Terisi 86% (status 2)",
            Network = "Wi-Fi: Intel Wi-Fi 6E AX211 160MHz (1.2 Gbps)",
            Security = "TPM: ada; Secure Boot: aktif; BitLocker (drive sistem): aktif",
            Firmware = "Demo Firmware 1.08 (20260915000000.000000+000)",
            Drivers = "Driver tidak bertanda tangan: 0",
        };

        Set(w, "processes", processes);
        Set(w, "processCount", processes.Count);
        Set(w, "startup", startup);
        Set(w, "startupCount", startup.Count(s => s.IsEnabled));
        Set(w, "startupTotal", startup.Count);
        Set(w, "apps", apps);
        Set(w, "cache", cache);
        Set(w, "system", system);
        Set(w, "memory", 41);
        Set(w, "cpu", (double?)12);
        Set(w, "free", 443_000_000_000L);
        Set(w, "capacity", 1_024_000_000_000L);
        Set(w, "drive", @"C:\");
        Set(w, "folder", u + @"\Downloads");
        Set(w, "lastCleanup", new LastCleanup(DateTime.Today.AddHours(9).AddMinutes(42), 4380, 2_941_000_000, false));

        var planType = w.GetType().GetNestedType("PowerPlan", BindingFlags.NonPublic)!;
        var plans = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(planType))!;
        plans.Add(Activator.CreateInstance(planType, new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"), "Balanced", true));
        plans.Add(Activator.CreateInstance(planType, new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"), "High performance", false));
        plans.Add(Activator.CreateInstance(planType, new Guid("a1841308-3541-4fab-bc81-f71556f20b4a"), "Power saver", false));
        Set(w, "plans", plans);
        Set(w, "selectedPlan", plans[0]);

        var results = (Dictionary<string, string>)w.GetType().GetField("results", Fields)!.GetValue(w)!;
        results["Internet dan DNS"] = string.Join(Environment.NewLine,
            "== Konfigurasi jaringan ==",
            "InterfaceAlias     : Wi-Fi",
            "IPv4Address        : 192.168.1.24",
            "IPv4DefaultGateway : 192.168.1.1",
            "DNSServer          : {1.1.1.1, 8.8.8.8}",
            "",
            "== Koneksi keluar (ping 1.1.1.1) ==",
            "OK: tersambung ke internet",
            "",
            "== DNS (microsoft.com) ==",
            "Name           Type IPAddress",
            "----           ---- ---------",
            "microsoft.com  A    20.70.246.20",
            "microsoft.com  A    20.236.44.162");

        var ready = (HashSet<string>)w.GetType().GetField("ready", Fields)!.GetValue(w)!;
        foreach (var key in keys) ready.Add(key);
    }

    // Menyamarkan sisa teks yang dibaca langsung dari Windows (nama komputer, nama pengguna di path).
    static void Anonymize(Window w)
    {
        string Clean(string s) => s.Replace(RealWindows, DemoWindows).Replace(Environment.MachineName, DemoMachine, StringComparison.OrdinalIgnoreCase)
            .Replace(@"\Users\" + Environment.UserName + @"\", @"\Users\" + DemoUser + @"\", StringComparison.OrdinalIgnoreCase);
        var root = (FrameworkElement)w.Content;
        Update(w);
        foreach (var t in Find<TextBlock>(w))
            if (Clean(t.Text) is var c && c != t.Text) t.Text = c;
        foreach (var t in Find<TextBox>(w))
            if (Clean(t.Text) is var c && c != t.Text) t.Text = c;
        ((TextBlock)root.FindName("Telemetry")).Text = "CPU 12%    RAM 41%    C: 412,58 GB bebas";
        ((TextBlock)root.FindName("Status")).Text = "Siap";
        Update(w);
        foreach (var t in Find<TextBlock>(w))
            Assert(!t.Text.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase) && !t.Text.Contains(@"\Users\" + Environment.UserName + @"\", StringComparison.OrdinalIgnoreCase), "Data pribadi tersisa: " + t.Text);
    }

    static void Readme(string output)
    {
        var type = typeof(MainWindow).Assembly.GetType("ZidWindowsApps.Fluent.LiveWindow", true)!;
        var w = (Window)Activator.CreateInstance(type, true)!;
        var timer = (DispatcherTimer)type.GetField("timer", Fields)!.GetValue(w)!;
        timer.Interval = TimeSpan.FromDays(1);
        timer.Stop();
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -20000;
        w.Width = 1366;
        w.Height = 820;
        w.Show();
        timer.Stop();
        // Konstruktor memuat dashboard asli secara asinkron: tunggu selesai, lalu timpa dengan data contoh.
        var loading = (HashSet<string>)type.GetField("loading", Fields)!.GetValue(w)!;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        do { Update(w); System.Threading.Thread.Sleep(50); } while (loading.Count > 0 && clock.Elapsed.TotalSeconds < 120);
        Inject(w);
        // README selalu memakai tema terang, apa pun preferensi yang tersimpan.
        App.Settings.DarkTheme = false;
        Call(w, "SetColors");

        void Shot(string route, string file)
        {
            Call(w, "Navigate", route);
            Anonymize(w);
            Capture(w, Path.Combine(output, file + ".png"));
            Console.WriteLine("PASS readme " + file);
        }

        Shot("dashboard", "dashboard");
        Shot("processes", "processes");
        Shot("autostart", "auto-start");
        Shot("uninstaller", "uninstaller");
        Set(w, "scanned", true);
        Shot("cleanup", "cleanup");
        Set(w, "scanned", false);
        Shot("sysinfo", "system-info");
        Shot("troubleshooting", "troubleshooting");
        Shot("tools", "tools");
        Shot("settings", "settings");
        w.Hide();
    }
}
