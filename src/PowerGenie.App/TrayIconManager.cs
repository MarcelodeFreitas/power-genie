using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace PowerGenie.App;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Icon? _currentPlanIcon;

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

    // Draws a simple colored dot so the tray icon itself shows which plan is active at a
    // glance, instead of always looking the same regardless of state.
    public void UpdateActivePlan(string planName, string hexColor)
    {
        var color = ColorTranslator.FromHtml(hexColor);

        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.White, 2);
            graphics.FillEllipse(brush, 3, 3, 26, 26);
            graphics.DrawEllipse(pen, 3, 3, 26, 26);
        }

        // Icon.FromHandle wraps the native handle without owning it — that handle must stay
        // alive for as long as this Icon is in use (assigned to _notifyIcon.Icon), and is only
        // destroyed once it's replaced by the next call, or on Dispose.
        var newIconHandle = bitmap.GetHicon();
        var newIcon = Icon.FromHandle(newIconHandle);

        var previousIcon = _currentPlanIcon;
        var previousHandle = previousIcon?.Handle ?? IntPtr.Zero;

        _notifyIcon.Icon = newIcon;
        _currentPlanIcon = newIcon;

        // NotifyIcon.Text is limited to 63 characters by the underlying Win32 API.
        var tooltip = $"Power Genie — {planName}";
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        if (previousIcon is not null)
        {
            previousIcon.Dispose();
            DestroyIcon(previousHandle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        if (_currentPlanIcon is not null)
        {
            var handle = _currentPlanIcon.Handle;
            _currentPlanIcon.Dispose();
            DestroyIcon(handle);
        }
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
