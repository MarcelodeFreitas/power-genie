namespace PowerGenie.App.Models;

public sealed class AppConfig
{
    public Guid DefaultPlanGuid { get; set; }
    public List<AppRule> Rules { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
    // Only plans whose icon the user customized; everything else is chosen automatically by
    // performance rank (see PlanIconDefaults).
    public Dictionary<Guid, PlanIconStyle> PlanIcons { get; set; } = new();
}
