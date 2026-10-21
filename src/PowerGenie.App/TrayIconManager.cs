using System.Drawing;
using PowerGenie.App.Models;
using PowerGenie.App.Services;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace PowerGenie.App;

// What the tray's GPU submenu needs. Only created when an NVIDIA GPU was found.
public sealed record GpuTrayMenu(
    Func<IReadOnlyList<GpuPreset>> GetPresets,
    Func<Guid> GetAppliedPresetId,
    Action<Guid> ApplyPreset);

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Icon? _currentPlanIcon;
    private readonly List<ToolStripItem> _activeRuleItems = new();
    private readonly ToolStripMenuItem? _gpuMenuItem;

    public TrayIconManager(Action onOpenSettings, Func<IReadOnlyList<string>> getActiveRuleLines, GpuTrayMenu? gpu = null)
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Opening += (_, _) =>
        {
            ShowActiveRuleLines(contextMenu, getActiveRuleLines());
            if (gpu is not null)
            {
                RefreshGpuPresets(gpu);
            }
        };

        if (gpu is not null)
        {
            _gpuMenuItem = new ToolStripMenuItem("GPU power limit");
            contextMenu.Items.Add(_gpuMenuItem);
        }

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

    // Rebuilt on every open, so the menu always names the rule(s) behind the current plan.
    private void ShowActiveRuleLines(ContextMenuStrip menu, IReadOnlyList<string> lines)
    {
        foreach (var item in _activeRuleItems)
        {
            menu.Items.Remove(item);
            item.Dispose();
        }
        _activeRuleItems.Clear();

        for (var i = 0; i < lines.Count; i++)
        {
            var item = new ToolStripMenuItem(lines[i]) { Enabled = false };
            menu.Items.Insert(i, item);
            _activeRuleItems.Add(item);
        }

        var separator = new ToolStripSeparator();
        menu.Items.Insert(lines.Count, separator);
        _activeRuleItems.Add(separator);
    }

    // Rebuilt on every open, so the presets are current and the applied one is checked.
    private void RefreshGpuPresets(GpuTrayMenu gpu)
    {
        // Disposing an item removes it from the menu, so the old items are copied out and cleared first.
        var items = _gpuMenuItem!.DropDownItems;
        var previous = items.Cast<ToolStripItem>().ToList();
        items.Clear();
        foreach (var item in previous)
        {
            item.Dispose();
        }

        var presets = gpu.GetPresets();
        if (presets.Count == 0)
        {
            items.Add(new ToolStripMenuItem("No presets - add one in Settings") { Enabled = false });
            return;
        }

        var applied = gpu.GetAppliedPresetId();
        foreach (var preset in presets)
        {
            var presetId = preset.Id;
            var item = new ToolStripMenuItem($"{preset.Name} - {preset.Watts} W") { Checked = presetId == applied };
            item.Click += (_, _) => gpu.ApplyPreset(presetId);
            items.Add(item);
        }
    }

    // Draws the plan's dot-and-symbol icon so the tray shows which plan is active at a glance,
    // instead of always looking the same regardless of state.
    public void UpdateActivePlan(string planName, PlanIconStyle style)
    {
        using var bitmap = TrayIconRenderer.Render(style, 32);

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
