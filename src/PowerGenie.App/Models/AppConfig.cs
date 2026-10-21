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
    public Guid GpuDefaultPresetId { get; set; }

    // Set once the shipped Silent and Balanced presets were added, so deleting them sticks.
    public bool GpuPresetsSeeded { get; set; }
}
