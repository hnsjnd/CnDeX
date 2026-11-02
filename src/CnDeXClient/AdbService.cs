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

    public static async Task<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(string adbPath, string serial)
    {
        var output = await RunAsync(adbPath, $"-s \"{serial}\" shell dumpsys display").ConfigureAwait(false);
        var displays = new Dictionary<int, DisplayInfo>();

        // Prefer DisplayDevice / uniqueId blocks when present
        var idMatches = Regex.Matches(output, @"mDisplayId\s*=\s*(\d+)");
        foreach (Match m in idMatches)
        {
            if (int.TryParse(m.Groups[1].Value, out var id))
            {
                displays[id] = new DisplayInfo { Id = id, Name = $"display-{id}" };
            }
        }

        // Enrich with names / uniqueIds near DisplayDevice sections
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

        // Alternate pattern: "Display id <n>:" headers
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

        // Fallback: highest non-zero display id (common when DeX just appeared)
        var other = list.Where(d => d.Id != 0).OrderByDescending(d => d.Id).FirstOrDefault();
        return other?.Id;
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
