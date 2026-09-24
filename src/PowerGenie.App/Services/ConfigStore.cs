using System.IO;
using System.Text.Json;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public ConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string GetDefaultFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerGenie");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch (JsonException)
        {
            // A malformed config (e.g. from a crash mid-write) must not take the app down on
            // every future launch. Preserve the bad file for inspection and start fresh.
            var badFilePath = _filePath + ".bad";
            File.Move(_filePath, badFilePath, overwrite: true);
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);

        // Write to a temp file and move it into place so a crash or power loss mid-write
        // can't leave a half-written, unparseable config.json behind.
        var tempFilePath = _filePath + ".tmp";
        File.WriteAllText(tempFilePath, json);
        File.Move(tempFilePath, _filePath, overwrite: true);
    }
}
