using System.IO;

namespace PowerGenie.App.Services;

public sealed class FileLogger
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileLogger(string filePath)
    {
        _filePath = filePath;
    }

    public static string GetDefaultFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerGenie");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "log.txt");
    }

    public void LogError(string message, Exception? exception = null)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}" +
                   (exception is null ? string.Empty : $" | {exception}");

        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllLines(_filePath, new[] { line });
        }
    }
}
