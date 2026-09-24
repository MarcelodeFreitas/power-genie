namespace PowerGenie.App.Models;

public sealed class AppConfig
{
    public Guid DefaultPlanGuid { get; set; }
    public List<AppRule> Rules { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
}
