using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZidWindowsApps;

public static class PowerShellService
{
    // Progress dimatikan (menghindari noise CLIXML) dan output dipaksa UTF-8 agar tidak rusak di Windows non-Inggris.
    private const string Prelude =
        "$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8; ";

    public static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    public static async Task<PowerShellResult> RunAsync(string command, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(Prelude + command));
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("powershell.exe tidak dapat dijalankan.");

        // stdout dan stderr dibaca bersamaan -> tidak ada deadlock saat salah satu pipe penuh.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException($"Perintah PowerShell melebihi batas waktu {timeoutSeconds} detik.");
        }

        return new PowerShellResult(process.ExitCode, Clean(await stdout.ConfigureAwait(false)), CleanError(await stderr.ConfigureAwait(false)));
    }

    /// <summary>Versi sinkron yang aman dipanggil dari thread mana pun (tidak membuat deadlock di UI thread).</summary>
    public static PowerShellResult Run(string command, int timeoutSeconds = 120) =>
        Task.Run(() => RunAsync(command, timeoutSeconds)).GetAwaiter().GetResult();

    private static string Clean(string text) => text.Replace("\0", "").Trim();

    private static string CleanError(string text)
    {
        text = Clean(text);
        if (!text.StartsWith("#< CLIXML", StringComparison.Ordinal)) return text;

        // Ambil hanya pesan error dari format CLIXML.
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(text, "<S S=\"Error\">(.*?)</S>", RegexOptions.Singleline))
            sb.Append(m.Groups[1].Value);
        return sb.ToString()
            .Replace("_x000D__x000A_", Environment.NewLine)
            .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"")
            .Trim();
    }
}
