using System;
using System.IO;
using System.Text.Json;

namespace CnDeXClient;

internal sealed class AppSettings
{
    public string ScrcpyPath { get; set; } = "";
    public string AdbPath { get; set; } = "";
    public string Resolution { get; set; } = "1920x1080";
    public string Dpi { get; set; } = "240";
    public string Bitrate { get; set; } = "8M";
    public string Fps { get; set; } = "60";
    public string LaunchMode { get; set; } = "new-display"; // new-display | display-id
    public string LastSerial { get; set; } = "";

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CnDeX", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // ignore corrupt settings
        }

        return new AppSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
