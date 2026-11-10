namespace PowerGenie.App.Models;

public sealed class AppConfig
{
    public Guid DefaultPlanGuid { get; set; }
    public List<AppRule> Rules { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
    // Only plans whose icon the user customized; everything else is chosen automatically by
    // performance rank (see PlanIconDefaults).
    public Dictionary<Guid, PlanIconStyle> PlanIcons { get; set; } = new();

    // GPU power limits. Separate from the Windows power plan settings above.
    public List<GpuPreset> GpuPresets { get; set; } = new();
    public List<GpuRule> GpuRules { get; set; } = new();

    // The preset each GPU returns to on exit, and keeps while no rule applies, keyed by GPU UUID.
    // A GPU without an entry returns to its original preset.
    public Dictionary<string, Guid> GpuDefaultPresetIds { get; set; } = new();

    // The GpuTasks.Version the scheduled tasks were registered with. Older values trigger a re-register.
    public int GpuTaskVersion { get; set; }
}
