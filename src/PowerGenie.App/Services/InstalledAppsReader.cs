using Microsoft.Win32;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class InstalledAppsReader
{
    private static readonly (RegistryKey Hive, string Path)[] UninstallKeyRoots =
    {
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    public static List<InstalledApp> GetInstalledApps()
    {
        var apps = new List<InstalledApp>();

        foreach (var (hive, keyPath) in UninstallKeyRoots)
        {
            using var uninstallKey = hive.OpenSubKey(keyPath);
            if (uninstallKey is null)
            {
                continue;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var appKey = uninstallKey.OpenSubKey(subKeyName);
                if (appKey is null)
                {
                    continue;
                }

                if (appKey.GetValue("DisplayName") is not string displayName || string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                // Skip OS components and update/patch entries — neither is something a user
                // would pick as "the app" to attach a power-plan rule to.
                if (appKey.GetValue("SystemComponent") is int isSystemComponent && isSystemComponent == 1)
                {
                    continue;
                }

                if (appKey.GetValue("ParentKeyName") is not null)
                {
                    continue;
                }

                var exePath = InstalledAppRegistryParser.TryResolveExePath(appKey.GetValue("DisplayIcon") as string);
                if (exePath is null)
                {
                    continue;
                }

                apps.Add(new InstalledApp(displayName, exePath));
            }
        }

        return apps
            .GroupBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
