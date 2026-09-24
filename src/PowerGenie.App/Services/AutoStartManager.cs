using Microsoft.Win32;

namespace PowerGenie.App.Services;

public sealed class AutoStartManager
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PowerGenie";

    private readonly string _runKeyPath;

    public AutoStartManager(string? runKeyPath = null)
    {
        _runKeyPath = runKeyPath ?? DefaultRunKeyPath;
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }
}
