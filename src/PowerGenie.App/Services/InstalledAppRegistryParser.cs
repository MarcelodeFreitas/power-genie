using System.IO;

namespace PowerGenie.App.Services;

public static class InstalledAppRegistryParser
{
    // "Programs and Features" registry entries store DisplayIcon as either a bare path
    // ("C:\...\app.exe") or a path with a trailing icon index ("C:\...\app.exe,0"). Many
    // entries point at a .ico/.dll or a path that no longer exists — those are deliberately
    // rejected rather than guessed at, so the installed-apps list only ever offers a real,
    // launchable .exe.
    public static string? TryResolveExePath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var path = displayIcon.Trim().Trim('"');
        var lastComma = path.LastIndexOf(',');
        if (lastComma >= 0 && int.TryParse(path[(lastComma + 1)..], out _))
        {
            path = path[..lastComma];
        }

        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(path) ? path : null;
    }
}
