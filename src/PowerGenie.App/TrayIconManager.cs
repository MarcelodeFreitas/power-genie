using System.Drawing;
using PowerGenie.App.Models;
using PowerGenie.App.Services;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace PowerGenie.App;

// What the tray's GPU submenus need. Only created when an NVIDIA GPU was found.
public sealed record GpuTrayMenu(
    Func<IReadOnlyList<GpuCardInfo>> GetCards,
    Func<IReadOnlyList<GpuPreset>> GetPresets,
    Func<string, Guid> GetAppliedPresetId,
    Action<Guid> ApplyPreset);

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Icon? _currentPlanIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripItem _settingsItem;
    private readonly List<ToolStripItem> _activeRuleItems = new();
    private readonly List<ToolStripMenuItem> _gpuMenuItems = new();

    public TrayIconManager(Action onOpenSettings, Func<IReadOnlyList<string>> getActiveRuleLines, GpuTrayMenu? gpu = null)
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Opening += (_, _) =>
        {
            ShowActiveRuleLines(_contextMenu, getActiveRuleLines());
            if (gpu is not null)
            {
                RefreshGpuPresets(gpu);
            }
        };

        // One submenu per GPU goes in front of Settings; RefreshGpuPresets places them on each open.
        _settingsItem = _contextMenu.Items.Add("Settings", null, (_, _) => onOpenSettings());
        _contextMenu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "Power Genie",
            ContextMenuStrip = _contextMenu,
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

    // Rebuilt on every open, so the presets are current and each GPU's applied preset is checked.
    // Every GPU gets its own submenu, headed by its label, so two identical cards can be told apart.
    private void RefreshGpuPresets(GpuTrayMenu gpu)
    {
        foreach (var item in _gpuMenuItems)
        {
            _contextMenu.Items.Remove(item);
            item.Dispose();
        }
        _gpuMenuItems.Clear();

        var presets = gpu.GetPresets();
        foreach (var card in gpu.GetCards())
        {
            var submenu = new ToolStripMenuItem(card.Label);
            var cardPresets = presets.Where(p => p.GpuUuid == card.Uuid).ToList();
            if (cardPresets.Count == 0)
            {
                submenu.DropDownItems.Add(new ToolStripMenuItem("No presets - add one in Settings") { Enabled = false });
            }

            var applied = gpu.GetAppliedPresetId(card.Uuid);
            foreach (var preset in cardPresets)
            {
                var presetId = preset.Id;
                // An original's name already has its watts, so they are not repeated.
                var text = preset.IsOriginal ? preset.Name : $"{preset.Name} - {preset.Watts} W";
                var item = new ToolStripMenuItem(text) { Checked = presetId == applied };
                item.Click += (_, _) => gpu.ApplyPreset(presetId);
                submenu.DropDownItems.Add(item);
            }

            _contextMenu.Items.Insert(_contextMenu.Items.IndexOf(_settingsItem), submenu);
            _gpuMenuItems.Add(submenu);
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
