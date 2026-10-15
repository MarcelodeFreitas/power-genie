using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class RuleResolverTests
{
    private static readonly Guid DefaultPlan = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid HighPerf = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly HashSet<Guid> AllPlans = new() { DefaultPlan, HighPerf, Balanced };

    [Fact]
    public void Returns_default_plan_when_no_rules_configured()
    {
        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "notepad.exe" },
            rules: new List<AppRule>(),
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(DefaultPlan, result);
    }

    [Fact]
    public void Returns_rule_plan_when_its_exe_is_running()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio", PlanGuid = HighPerf }
        };

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "explorer.exe", "bambu-studio.exe" },
            rules: rules,
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(HighPerf, result);
    }

    [Fact]
    public void First_matching_rule_in_list_order_wins_when_multiple_apps_run()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio", PlanGuid = HighPerf },
            new() { ExeName = "notepad.exe", DisplayName = "Notepad", PlanGuid = Balanced }
        };

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "notepad.exe", "bambu-studio.exe" },
            rules: rules,
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(HighPerf, result);
    }

    [Fact]
    public void Skips_a_rule_whose_plan_no_longer_exists_and_falls_through()
    {
        var deletedPlan = Guid.NewGuid();
        var rules = new List<AppRule>
        {
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio (deleted plan)", PlanGuid = deletedPlan },
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio (fallback)", PlanGuid = HighPerf }
        };

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "bambu-studio.exe" },
            rules: rules,
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(HighPerf, result);
    }

    [Fact]
    public void Returns_guid_empty_when_the_default_plan_no_longer_exists()
    {
        // Guid.Empty is the sentinel ProcessMonitorService already treats as "do nothing" -
        // returning it here means a stale/invalid default can't cause a doomed powercfg call
        // (and the resulting log spam) on every single tick forever.
        var deletedDefault = Guid.NewGuid();

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "notepad.exe" },
            rules: new List<AppRule>(),
            availablePlanGuids: AllPlans,
            defaultPlanGuid: deletedDefault);

        Assert.Equal(Guid.Empty, result);
    }

    [Fact]
    public void Exe_name_matching_is_case_insensitive()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio", PlanGuid = HighPerf }
        };

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "Bambu-Studio.EXE" },
            rules: rules,
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(HighPerf, result);
    }

    [Fact]
    public void Highest_performance_plan_wins_when_several_rules_match()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "light.exe", DisplayName = "Light", PlanGuid = Balanced },
            new() { ExeName = "heavy.exe", DisplayName = "Heavy", PlanGuid = HighPerf }
        };
        var scores = new Dictionary<Guid, int> { [Balanced] = 100_005, [HighPerf] = 100_100, [DefaultPlan] = 50_005 };

        var result = RuleResolver.ResolveActivePlan(
            new[] { "light.exe", "heavy.exe" }, rules, AllPlans, DefaultPlan, scores);

        Assert.Equal(HighPerf, result);
    }

    [Fact]
    public void Equal_scores_fall_back_to_rule_order()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "a.exe", DisplayName = "A", PlanGuid = Balanced },
            new() { ExeName = "b.exe", DisplayName = "B", PlanGuid = HighPerf }
        };
        var scores = new Dictionary<Guid, int> { [Balanced] = 100_100, [HighPerf] = 100_100 };

        var result = RuleResolver.ResolveActivePlan(
            new[] { "b.exe", "a.exe" }, rules, AllPlans, DefaultPlan, scores);

        Assert.Equal(Balanced, result);
    }

    [Fact]
    public void Plans_without_a_score_rank_lowest()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "a.exe", DisplayName = "A", PlanGuid = Balanced },
            new() { ExeName = "b.exe", DisplayName = "B", PlanGuid = HighPerf }
        };
        var scores = new Dictionary<Guid, int> { [HighPerf] = 100_100 };

        var result = RuleResolver.ResolveActivePlan(
            new[] { "a.exe", "b.exe" }, rules, AllPlans, DefaultPlan, scores);

        Assert.Equal(HighPerf, result);
    }

    [Fact]
    public void A_disabled_rule_is_skipped_and_the_next_matching_enabled_rule_wins()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio", PlanGuid = HighPerf, IsEnabled = false },
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio (fallback)", PlanGuid = Balanced, IsEnabled = true }
        };

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "bambu-studio.exe" },
            rules: rules,
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(Balanced, result);
    }

    [Fact]
    public void A_disabled_rule_is_ignored_when_it_is_the_only_match_and_default_plan_is_returned()
    {
        var rules = new List<AppRule>
        {
            new() { ExeName = "bambu-studio.exe", DisplayName = "Bambu Studio", PlanGuid = HighPerf, IsEnabled = false }
        };

        var result = RuleResolver.ResolveActivePlan(
            runningExeNames: new[] { "bambu-studio.exe" },
            rules: rules,
            availablePlanGuids: AllPlans,
            defaultPlanGuid: DefaultPlan);

        Assert.Equal(DefaultPlan, result);
    }
}
