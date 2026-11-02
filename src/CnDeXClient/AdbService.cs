using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CnDeXClient;

internal sealed class AdbDevice
{
    public string Serial { get; init; } = "";
    public string State { get; init; } = "";
    public string Model { get; init; } = "";
    public string Transport { get; init; } = "";

    public override string ToString()
    {
        var label = string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";
        if (!string.IsNullOrWhiteSpace(Transport))
            label += $" [{Transport}]";
        if (!string.Equals(State, "device", StringComparison.OrdinalIgnoreCase))
            label += $" - {State}";
        return label;
    }
}

internal sealed class DisplayInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string UniqueId { get; init; } = "";
}

internal static class AdbService
{
    public static string? ResolveAdbPath(string? userPath, string? scrcpyPath)
    {
        if (!string.IsNullOrWhiteSpace(userPath) && File.Exists(userPath))
            return userPath;

        if (!string.IsNullOrWhiteSpace(scrcpyPath))
        {
            var dir = Path.GetDirectoryName(scrcpyPath);
            if (!string.IsNullOrEmpty(dir))
            {
                var beside = Path.Combine(dir, "adb.exe");
                if (File.Exists(beside))
                    return beside;
            }
        }

        var fromPath = FindOnPath("adb.exe") ?? FindOnPath("adb");
        return fromPath;
    }

    public static string? ResolveScrcpyPath(string? userPath)
    {
        if (!string.IsNullOrWhiteSpace(userPath) && File.Exists(userPath))
            return userPath;
        return FindOnPath("scrcpy.exe") ?? FindOnPath("scrcpy");
    }

    private static string? FindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // skip bad PATH entries
            }
        }

        return null;
    }

    public static async Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(string adbPath)
    {
        var output = await RunAsync(adbPath, "devices -l").ConfigureAwait(false);
        var list = new List<AdbDevice>();
        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                continue;
            if (raw.StartsWith("*", StringComparison.Ordinal))
                continue;

            var parts = Regex.Split(raw, @"\s+");
            if (parts.Length < 2)
                continue;

            var serial = parts[0];
            var state = parts[1];
            string model = "";
            string transport = serial.Contains(':') ? "无线" : "USB";
            foreach (var p in parts.Skip(2))
            {
                if (p.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                    model = p["model:".Length..].Replace('_', ' ');
                else if (p.StartsWith("transport_id:", StringComparison.OrdinalIgnoreCase))
                {
                    // keep transport inferred from serial
                }
            }

            list.Add(new AdbDevice
            {
                Serial = serial,
                State = state,
                Model = model,
                Transport = transport
            });
        }

        return list;
    }

    /// <summary>
    /// Android 11+ wireless debugging: adb pair IP:PAIR_PORT CODE
    /// </summary>
    public static async Task<(bool Ok, string Output)> PairAsync(string adbPath, string hostPort, string pairingCode)
    {
        var code = pairingCode.Trim().Replace(" ", "");
        var output = await RunAsync(adbPath, $"pair {Quote(hostPort)} {Quote(code)}", timeoutMs: 60000).ConfigureAwait(false);
        var ok = output.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase)
                 || output.Contains("成功", StringComparison.Ordinal);
        return (ok, output.Trim());
    }

    /// <summary>
    /// adb connect IP:PORT
    /// </summary>
    public static async Task<(bool Ok, string Output)> ConnectAsync(string adbPath, string hostPort)
    {
        var output = await RunAsync(adbPath, $"connect {Quote(hostPort)}", timeoutMs: 30000).ConfigureAwait(false);
        var ok = output.Contains("connected to", StringComparison.OrdinalIgnoreCase)
                 || output.Contains("already connected", StringComparison.OrdinalIgnoreCase);
        return (ok, output.Trim());
    }

    public static async Task<string> DisconnectAsync(string adbPath, string? hostPort = null)
    {
        var args = string.IsNullOrWhiteSpace(hostPort) ? "disconnect" : $"disconnect {Quote(hostPort)}";
        return (await RunAsync(adbPath, args, timeoutMs: 15000).ConfigureAwait(false)).Trim();
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains('"') || value.Contains(' ') || value.Contains('&'))
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        return value;
    }

    public static async Task<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(string adbPath, string serial)
    {
        var output = await RunAsync(adbPath, $"-s \"{serial}\" shell dumpsys display").ConfigureAwait(false);
        var displays = new Dictionary<int, DisplayInfo>();

        var idMatches = Regex.Matches(output, @"mDisplayId\s*=\s*(\d+)");
        foreach (Match m in idMatches)
        {
            if (int.TryParse(m.Groups[1].Value, out var id))
            {
                displays[id] = new DisplayInfo { Id = id, Name = $"display-{id}" };
            }
        }

        var blocks = Regex.Matches(
            output,
            @"DisplayDeviceInfo\{[^}]*name=(?<name>[^,\}]+)[^}]*uniqueId=(?<uid>[^,\}]+)[^}]*displayId=(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in blocks)
        {
            if (!int.TryParse(m.Groups["id"].Value, out var id))
                continue;
            displays[id] = new DisplayInfo
            {
                Id = id,
                Name = m.Groups["name"].Value.Trim(),
                UniqueId = m.Groups["uid"].Value.Trim()
            };
        }

        foreach (Match m in Regex.Matches(output, @"Display id\s+(\d+):", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(m.Groups[1].Value, out var id) && !displays.ContainsKey(id))
                displays[id] = new DisplayInfo { Id = id, Name = $"display-{id}" };
        }

        return displays.Values.OrderBy(d => d.Id).ToList();
    }

    /// <summary>
    /// Pick a likely DeX / virtual / external display (not the built-in 0).
    /// </summary>
    public static int? FindDexDisplayId(IEnumerable<DisplayInfo> displays)
    {
        var list = displays.ToList();
        static bool LooksLikeDex(DisplayInfo d)
        {
            var hay = $"{d.Name} {d.UniqueId}".ToLowerInvariant();
            return hay.Contains("dex")
                   || hay.Contains("desktop")
                   || hay.Contains("virtual")
                   || hay.Contains("overlay")
                   || hay.Contains("hdmi")
                   || hay.Contains("wifi display")
                   || hay.Contains("scrcpy");
        }

        var named = list.Where(d => d.Id != 0 && LooksLikeDex(d)).OrderByDescending(d => d.Id).FirstOrDefault();
        if (named != null)
            return named.Id;

        var other = list.Where(d => d.Id != 0).OrderByDescending(d => d.Id).FirstOrDefault();
        return other?.Id;
    }

    /// <summary>
    /// Map sensitivity 1–10 onto a DPI for --new-display.
    /// Lower sensitivity → higher DPI (UI denser / pointer travels farther on screen).
    /// Higher sensitivity → lower DPI (UI larger / less travel for same mouse move with UHID).
    /// When baseDpi is set, scale around it; otherwise use absolute presets.
    /// </summary>
    public static int SensitivityToDpi(int sensitivity, int? baseDpi = null)
    {
        var s = Math.Clamp(sensitivity, 1, 10);
        // Presets: 1=慢( denser / smaller pointer travel feel) … 10=快
        // With UHID, Android pointer speed + display DPI affect perceived speed.
        // Approximation: lower DPI → larger UI elements → same physical mouse move covers less "content".
        // User expectation: higher slider = faster → lower DPI.
        int[] presets = { 400, 360, 320, 284, 240, 220, 200, 180, 160, 140 };
        var fromPreset = presets[s - 1];
        if (baseDpi is null or <= 0)
            return fromPreset;

        // Scale relative to user base: sensitivity 5 = base, each step ±8%
        var factor = 1.0 + (5 - s) * 0.08;
        var dpi = (int)Math.Round(baseDpi.Value * factor);
        return Math.Clamp(dpi, 120, 480);
    }

    public static async Task<string> RunAsync(string exePath, string args, int timeoutMs = 20000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动: {exePath}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        var finished = await Task.Run(() => proc.WaitForExit(timeoutMs)).ConfigureAwait(false);
        if (!finished)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException($"命令超时: {Path.GetFileName(exePath)} {args}");
        }

        return sb.ToString();
    }
}
