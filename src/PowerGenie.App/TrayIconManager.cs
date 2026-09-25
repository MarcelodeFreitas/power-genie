using System.Windows.Forms;
using Application = System.Windows.Application;

namespace PowerGenie.App;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconManager(Action onOpenSettings)
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Settings", null, (_, _) => onOpenSettings());
        contextMenu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "Power Genie",
            ContextMenuStrip = contextMenu,
            Visible = false
        };
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        // Extracted from the executable itself (embedded via <ApplicationIcon> in the csproj),
        // so it stays in sync with the .exe icon and works from a single-file publish too.
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var icon = exePath is not null ? System.Drawing.Icon.ExtractAssociatedIcon(exePath) : null;
        return icon ?? System.Drawing.SystemIcons.Application;
    }
}
