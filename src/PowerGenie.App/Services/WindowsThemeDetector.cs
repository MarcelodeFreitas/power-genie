using Microsoft.Win32;

namespace PowerGenie.App.Services;

public static class WindowsThemeDetector
{
    public static bool IsDarkModeEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

        // Missing key/value (older Windows builds without this personalization setting)
        // defaults to light mode, matching Windows' own behavior before the setting existed.
        return key?.GetValue("AppsUseLightTheme") is int lightThemeFlag && lightThemeFlag == 0;
    }
}
