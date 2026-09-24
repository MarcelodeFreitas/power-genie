# Power Genie v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a Windows tray app that lets the user pick a default power plan and define per-app rules, and automatically switches the active Windows power plan based on which tracked apps are currently running.

**Architecture:** A single C#/.NET WPF app (`PowerGenie.App`) with a tray icon and no visible main window. A background timer polls running processes every few seconds, resolves the plan that should be active via a pure `RuleResolver`, and calls `powercfg.exe` only when that plan changes. Settings (default plan, rules, autostart) persist to a JSON file under `%AppData%\PowerGenie`. A parallel xUnit test project (`PowerGenie.Tests`) covers every piece of logic that doesn't require a live UI or a real timer.

**Tech Stack:** .NET 8 (`net8.0-windows`), WPF + WinForms `NotifyIcon` for the tray icon, `System.Text.Json` for config persistence, `Microsoft.Win32.Registry` for autostart, xUnit for tests.

**Spec:** [docs/design.md](../design.md)

## Prerequisites

The .NET SDK is **not currently installed** on this machine (only the runtime host exists, no SDK — confirmed via `dotnet --list-sdks`). Before Task 1:

- [ ] **Install the .NET 8 SDK.** Ask the user to confirm before running (installs system-wide software via `winget`):

```bash
winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
```

- [ ] Close and reopen the terminal (or start a new shell) so `PATH` picks up the new SDK, then verify:

```bash
dotnet --list-sdks
```

Expected: a line showing an `8.x.x` SDK.

## Global Constraints

- Target framework: `net8.0-windows` for both the app and test projects (the app is Windows-only; the test project references it directly, so it must share a compatible TFM).
- No admin rights required anywhere: autostart uses `HKCU\...\Run`, never `HKLM`.
- Config lives at `%AppData%\PowerGenie\config.json`; log lives at `%AppData%\PowerGenie\log.txt`. Never write app data anywhere else.
- No NuGet packages beyond the test SDK (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`) — everything else needed (WPF, WinForms, `Microsoft.Win32.Registry`, `System.Text.Json`) ships with the `net8.0-windows` SDK.
- License is MIT (already in the repo) — no code here changes that.

## Review Focus

- Two tracked apps running at the same time must resolve to one deterministic plan, not throw or flip-flop — covered by Task 3's tie-break test.
- A rule pointing at a power plan the user later deletes in Windows must not crash the monitor loop or the Settings UI — covered by Task 3's "unknown plan" test and Task 9's "(deleted plan)" display fallback.
- Windows process names can be reported with different casing than what the user typed/browsed to — exe matching must be case-insensitive — covered by Task 3's case-insensitivity test.
- Enumerating `Process.GetProcesses()` hits processes the current user can't inspect (`MainModule` throws `Win32Exception` for elevated/system processes) — this must not crash monitoring — covered by Task 8's per-process try/catch and the manual verification step.
- First run with no `config.json` yet must not crash — must start from sane defaults (empty rules, no default plan selected until the user picks one) — covered by Task 4's "file does not exist" test.

---

### Task 1: Solution & project scaffolding (walking skeleton)

**Files:**
- Create: `PowerGenie.sln`
- Create: `src/PowerGenie.App/PowerGenie.App.csproj`
- Create: `src/PowerGenie.App/App.xaml`
- Create: `src/PowerGenie.App/App.xaml.cs`
- Create: `src/PowerGenie.App/TrayIconManager.cs`
- Create: `tests/PowerGenie.Tests/PowerGenie.Tests.csproj`
- Create: `tests/PowerGenie.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: `PowerGenie.App.TrayIconManager` — `TrayIconManager(Action onOpenSettings)`, `.Show()`, `IDisposable`. (The `onOpenSettings` callback is unused until Task 10; pass `() => { }` for now everywhere it's constructed in this task.)

- [ ] **Step 1: Create the solution and both projects**

```bash
dotnet new sln -n PowerGenie
dotnet new wpf -o src/PowerGenie.App -n PowerGenie.App --use-windows-forms true
dotnet new xunit -o tests/PowerGenie.Tests -n PowerGenie.Tests
dotnet sln add src/PowerGenie.App/PowerGenie.App.csproj tests/PowerGenie.Tests/PowerGenie.Tests.csproj
dotnet add tests/PowerGenie.Tests/PowerGenie.Tests.csproj reference src/PowerGenie.App/PowerGenie.App.csproj
```

- [ ] **Step 2: Set the assembly name and root namespace in `src/PowerGenie.App/PowerGenie.App.csproj`**

Open the generated file and make the `<PropertyGroup>` read:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <UseWPF>true</UseWPF>
  <UseWindowsForms>true</UseWindowsForms>
  <ImplicitUsings>enable</ImplicitUsings>
  <RootNamespace>PowerGenie.App</RootNamespace>
  <AssemblyName>PowerGenie</AssemblyName>
</PropertyGroup>
```

- [ ] **Step 3: Replace `src/PowerGenie.App/App.xaml` contents**

```xml
<Application x:Class="PowerGenie.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
</Application>
```

`ShutdownMode="OnExplicitShutdown"` is required — this app has no main window, so the default `OnLastWindowClose` would exit immediately on startup.

- [ ] **Step 4: Replace `src/PowerGenie.App/App.xaml.cs` contents**

```csharp
using System.Windows;

namespace PowerGenie.App;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIconManager = new TrayIconManager(() => { });
        _trayIconManager.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 5: Create `src/PowerGenie.App/TrayIconManager.cs`**

```csharp
using System.Windows;
using System.Windows.Forms;

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
            Icon = System.Drawing.SystemIcons.Application,
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
}
```

- [ ] **Step 6: Replace the generated test file `tests/PowerGenie.Tests/SmokeTests.cs`** (delete `UnitTest1.cs` if the template created one)

```csharp
namespace PowerGenie.Tests;

public class SmokeTests
{
    [Fact]
    public void Test_project_can_see_the_app_project()
    {
        var manager = new PowerGenie.App.TrayIconManager(() => { });
        manager.Dispose();
    }
}
```

- [ ] **Step 7: Build and test**

```bash
dotnet build PowerGenie.sln
dotnet test PowerGenie.sln
```

Expected: build succeeds, 1 test passes.

- [ ] **Step 8: Manual check — the tray icon actually shows up**

```bash
dotnet run --project src/PowerGenie.App
```

Expected: no visible window, but an icon appears in the system tray; right-click shows "Settings" (does nothing yet) and "Exit" (closes the app). Stop it via Exit before continuing.

- [ ] **Step 9: Commit**

```bash
git add PowerGenie.sln src/ tests/
git commit -m "Scaffold WPF app + test project with a walking-skeleton tray icon"
```

---

### Task 2: Models + power plan list parsing

**Files:**
- Create: `src/PowerGenie.App/Models/PowerPlan.cs`
- Create: `src/PowerGenie.App/Models/AppRule.cs`
- Create: `src/PowerGenie.App/Models/AppConfig.cs`
- Create: `src/PowerGenie.App/Services/PowerPlanListParser.cs`
- Create: `tests/PowerGenie.Tests/PowerPlanListParserTests.cs`

**Interfaces:**
- Produces: `PowerGenie.App.Models.PowerPlan` — `record PowerPlan(Guid Guid, string Name, bool IsActive)`.
- Produces: `PowerGenie.App.Models.AppRule` — `class AppRule { string ExeName; string DisplayName; Guid PlanGuid; }`.
- Produces: `PowerGenie.App.Models.AppConfig` — `class AppConfig { Guid DefaultPlanGuid; List<AppRule> Rules; bool StartWithWindows = true; }`.
- Produces: `PowerGenie.App.Services.PowerPlanListParser.Parse(string) -> List<PowerPlan>`.

- [ ] **Step 1: Create the models**

`src/PowerGenie.App/Models/PowerPlan.cs`:
```csharp
namespace PowerGenie.App.Models;

public sealed record PowerPlan(Guid Guid, string Name, bool IsActive);
```

`src/PowerGenie.App/Models/AppRule.cs`:
```csharp
namespace PowerGenie.App.Models;

public sealed class AppRule
{
    public required string ExeName { get; set; }
    public required string DisplayName { get; set; }
    public required Guid PlanGuid { get; set; }
}
```

`src/PowerGenie.App/Models/AppConfig.cs`:
```csharp
namespace PowerGenie.App.Models;

public sealed class AppConfig
{
    public Guid DefaultPlanGuid { get; set; }
    public List<AppRule> Rules { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
}
```

- [ ] **Step 2: Write the failing tests for the parser**

`tests/PowerGenie.Tests/PowerPlanListParserTests.cs`:
```csharp
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PowerPlanListParserTests
{
    private const string SampleOutput =
        "Existing Power Schemes (* Active)\r\n" +
        "-----------------------------------\r\n" +
        "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)\r\n" +
        "Power Scheme GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (High performance)\r\n" +
        "Power Scheme GUID: a1841308-3541-4fab-bc81-f71556f20b4a  (Power saver) *\r\n";

    [Fact]
    public void Parses_all_schemes_from_powercfg_list_output()
    {
        var plans = PowerPlanListParser.Parse(SampleOutput);

        Assert.Equal(3, plans.Count);
        Assert.Equal("Balanced", plans[0].Name);
        Assert.Equal(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), plans[0].Guid);
    }

    [Fact]
    public void Marks_the_scheme_with_a_trailing_asterisk_as_active()
    {
        var plans = PowerPlanListParser.Parse(SampleOutput);

        var active = plans.Single(p => p.IsActive);
        Assert.Equal("Power saver", active.Name);
    }

    [Fact]
    public void Ignores_header_and_separator_lines()
    {
        var plans = PowerPlanListParser.Parse(SampleOutput);

        Assert.All(plans, p => Assert.NotEqual(Guid.Empty, p.Guid));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail (compile error — parser doesn't exist yet)**

```bash
dotnet test PowerGenie.sln
```

Expected: FAIL — `PowerPlanListParser` does not exist.

- [ ] **Step 4: Implement the parser**

`src/PowerGenie.App/Services/PowerPlanListParser.cs`:
```csharp
using System.Text.RegularExpressions;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class PowerPlanListParser
{
    private static readonly Regex LineRegex = new(
        @"Power Scheme GUID:\s*(?<guid>[0-9a-fA-F-]{36})\s*\((?<name>.+?)\)\s*(?<active>\*)?\s*$",
        RegexOptions.Compiled);

    public static List<PowerPlan> Parse(string powercfgListOutput)
    {
        var plans = new List<PowerPlan>();

        foreach (var rawLine in powercfgListOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var match = LineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            plans.Add(new PowerPlan(
                Guid.Parse(match.Groups["guid"].Value),
                match.Groups["name"].Value.Trim(),
                match.Groups["active"].Success));
        }

        return plans;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test PowerGenie.sln
```

Expected: PASS — all `PowerPlanListParserTests` green.

- [ ] **Step 6: Commit**

```bash
git add src/PowerGenie.App/Models src/PowerGenie.App/Services/PowerPlanListParser.cs tests/PowerGenie.Tests/PowerPlanListParserTests.cs
git commit -m "Add config models and powercfg /list output parser"
```

---

### Task 3: RuleResolver (pure decision logic)

**Files:**
- Create: `src/PowerGenie.App/Services/RuleResolver.cs`
- Create: `tests/PowerGenie.Tests/RuleResolverTests.cs`

**Interfaces:**
- Consumes: `PowerGenie.App.Models.AppRule` (Task 2).
- Produces: `PowerGenie.App.Services.RuleResolver.ResolveActivePlan(IReadOnlyList<string> runningExeNames, IReadOnlyList<AppRule> rules, IReadOnlySet<Guid> availablePlanGuids, Guid defaultPlanGuid) -> Guid`.

- [ ] **Step 1: Write the failing tests**

`tests/PowerGenie.Tests/RuleResolverTests.cs`:
```csharp
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test PowerGenie.sln
```

Expected: FAIL — `RuleResolver` does not exist.

- [ ] **Step 3: Implement `RuleResolver`**

`src/PowerGenie.App/Services/RuleResolver.cs`:
```csharp
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public static class RuleResolver
{
    public static Guid ResolveActivePlan(
        IReadOnlyList<string> runningExeNames,
        IReadOnlyList<AppRule> rules,
        IReadOnlySet<Guid> availablePlanGuids,
        Guid defaultPlanGuid)
    {
        var runningSet = new HashSet<string>(runningExeNames, StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (!availablePlanGuids.Contains(rule.PlanGuid))
            {
                continue;
            }

            if (runningSet.Contains(rule.ExeName))
            {
                return rule.PlanGuid;
            }
        }

        return defaultPlanGuid;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test PowerGenie.sln
```

Expected: PASS — all `RuleResolverTests` green.

- [ ] **Step 5: Commit**

```bash
git add src/PowerGenie.App/Services/RuleResolver.cs tests/PowerGenie.Tests/RuleResolverTests.cs
git commit -m "Add RuleResolver: pure logic for which plan should be active"
```

---

### Task 4: ConfigStore (JSON persistence)

**Files:**
- Create: `src/PowerGenie.App/Services/ConfigStore.cs`
- Create: `tests/PowerGenie.Tests/ConfigStoreTests.cs`

**Interfaces:**
- Consumes: `PowerGenie.App.Models.AppConfig`, `AppRule` (Task 2).
- Produces: `PowerGenie.App.Services.ConfigStore` — `ConfigStore(string filePath)`, `static string GetDefaultFilePath()`, `AppConfig Load()`, `void Save(AppConfig config)`.

- [ ] **Step 1: Write the failing tests**

`tests/PowerGenie.Tests/ConfigStoreTests.cs`:
```csharp
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempFilePath =
        Path.Combine(Path.GetTempPath(), $"power-genie-test-{Guid.NewGuid()}.json");

    [Fact]
    public void Load_returns_default_config_when_file_does_not_exist()
    {
        var store = new ConfigStore(_tempFilePath);

        var config = store.Load();

        Assert.Equal(Guid.Empty, config.DefaultPlanGuid);
        Assert.Empty(config.Rules);
        Assert.True(config.StartWithWindows);
    }

    [Fact]
    public void Save_then_load_round_trips_all_fields()
    {
        var store = new ConfigStore(_tempFilePath);
        var original = new AppConfig
        {
            DefaultPlanGuid = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a"),
            StartWithWindows = false,
            Rules = new List<AppRule>
            {
                new()
                {
                    ExeName = "bambu-studio.exe",
                    DisplayName = "Bambu Studio",
                    PlanGuid = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")
                }
            }
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.DefaultPlanGuid, loaded.DefaultPlanGuid);
        Assert.False(loaded.StartWithWindows);
        Assert.Single(loaded.Rules);
        Assert.Equal("bambu-studio.exe", loaded.Rules[0].ExeName);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test PowerGenie.sln
```

Expected: FAIL — `ConfigStore` does not exist.

- [ ] **Step 3: Implement `ConfigStore`**

`src/PowerGenie.App/Services/ConfigStore.cs`:
```csharp
using System.Text.Json;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public ConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string GetDefaultFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerGenie");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppConfig();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test PowerGenie.sln
```

Expected: PASS — all `ConfigStoreTests` green.

- [ ] **Step 5: Commit**

```bash
git add src/PowerGenie.App/Services/ConfigStore.cs tests/PowerGenie.Tests/ConfigStoreTests.cs
git commit -m "Add ConfigStore: JSON persistence for app settings"
```

---

### Task 5: PowerPlanService (powercfg.exe wrapper)

**Files:**
- Create: `src/PowerGenie.App/Services/PowerPlanService.cs`
- Create: `tests/PowerGenie.Tests/PowerPlanServiceTests.cs`

**Interfaces:**
- Consumes: `PowerGenie.App.Services.PowerPlanListParser.Parse` (Task 2).
- Produces: `PowerGenie.App.Services.PowerPlanService` — `List<PowerPlan> GetAvailablePlans()`, `void SetActivePlan(Guid planGuid)`.

**Important:** `SetActivePlan` changes the *real, active Windows power plan on whatever machine runs it*. It must never be called from an automated test — only `GetAvailablePlans` (read-only) gets a test here.

- [ ] **Step 1: Write the failing test (read-only call only)**

`tests/PowerGenie.Tests/PowerPlanServiceTests.cs`:
```csharp
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class PowerPlanServiceTests
{
    [Fact]
    public void GetAvailablePlans_returns_the_real_schemes_configured_on_this_machine()
    {
        var service = new PowerPlanService();

        var plans = service.GetAvailablePlans();

        Assert.NotEmpty(plans);
        Assert.Contains(plans, p => p.IsActive);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test PowerGenie.sln
```

Expected: FAIL — `PowerPlanService` does not exist.

- [ ] **Step 3: Implement `PowerPlanService`**

`src/PowerGenie.App/Services/PowerPlanService.cs`:
```csharp
using System.Diagnostics;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class PowerPlanService
{
    public List<PowerPlan> GetAvailablePlans()
    {
        var output = RunPowercfg("/list");
        return PowerPlanListParser.Parse(output);
    }

    public void SetActivePlan(Guid planGuid)
    {
        RunPowercfg($"/setactive {planGuid:D}");
    }

    private static string RunPowercfg(string arguments)
    {
        var startInfo = new ProcessStartInfo("powercfg", arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start powercfg.exe");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test PowerGenie.sln
```

Expected: PASS — `PowerPlanServiceTests` green (this actually shells out to `powercfg /list` on the machine running the tests — read-only, safe).

- [ ] **Step 5: Manual check — `SetActivePlan` really switches the plan**

```bash
powercfg /list
```

Note the GUID of a plan that is *not* currently active, then run a short throwaway script (or use `dotnet fsi`/a scratch `Main`) calling `new PowerPlanService().SetActivePlan(thatGuid)`, then run `powercfg /list` again and confirm the `*` moved. Set it back to Power saver afterward.

- [ ] **Step 6: Commit**

```bash
git add src/PowerGenie.App/Services/PowerPlanService.cs tests/PowerGenie.Tests/PowerPlanServiceTests.cs
git commit -m "Add PowerPlanService: list and switch the active Windows power plan"
```

---

### Task 6: FileLogger

**Files:**
- Create: `src/PowerGenie.App/Services/FileLogger.cs`
- Create: `tests/PowerGenie.Tests/FileLoggerTests.cs`

**Interfaces:**
- Produces: `PowerGenie.App.Services.FileLogger` — `FileLogger(string filePath)`, `static string GetDefaultFilePath()`, `void LogError(string message, Exception? exception = null)`.

- [ ] **Step 1: Write the failing tests**

`tests/PowerGenie.Tests/FileLoggerTests.cs`:
```csharp
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class FileLoggerTests : IDisposable
{
    private readonly string _tempFilePath =
        Path.Combine(Path.GetTempPath(), $"power-genie-log-{Guid.NewGuid()}.txt");

    [Fact]
    public void LogError_appends_a_line_containing_the_message()
    {
        var logger = new FileLogger(_tempFilePath);

        logger.LogError("something failed");

        var contents = File.ReadAllText(_tempFilePath);
        Assert.Contains("something failed", contents);
        Assert.Contains("[ERROR]", contents);
    }

    [Fact]
    public void LogError_appends_multiple_entries_without_overwriting()
    {
        var logger = new FileLogger(_tempFilePath);

        logger.LogError("first");
        logger.LogError("second");

        var lines = File.ReadAllLines(_tempFilePath);
        Assert.Equal(2, lines.Length);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test PowerGenie.sln
```

Expected: FAIL — `FileLogger` does not exist.

- [ ] **Step 3: Implement `FileLogger`**

`src/PowerGenie.App/Services/FileLogger.cs`:
```csharp
namespace PowerGenie.App.Services;

public sealed class FileLogger
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileLogger(string filePath)
    {
        _filePath = filePath;
    }

    public static string GetDefaultFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PowerGenie");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "log.txt");
    }

    public void LogError(string message, Exception? exception = null)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}" +
                   (exception is null ? string.Empty : $" | {exception}");

        lock (_lock)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllLines(_filePath, new[] { line });
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test PowerGenie.sln
```

Expected: PASS — `FileLoggerTests` green.

- [ ] **Step 5: Commit**

```bash
git add src/PowerGenie.App/Services/FileLogger.cs tests/PowerGenie.Tests/FileLoggerTests.cs
git commit -m "Add FileLogger for background-monitor error logging"
```

---

### Task 7: AutoStartManager (HKCU Run key)

**Files:**
- Create: `src/PowerGenie.App/Services/AutoStartManager.cs`
- Create: `tests/PowerGenie.Tests/AutoStartManagerTests.cs`

**Interfaces:**
- Produces: `PowerGenie.App.Services.AutoStartManager` — `AutoStartManager(string? runKeyPath = null)`, `void SetEnabled(bool enabled, string executablePath)`, `bool IsEnabled()`.

The constructor takes an optional registry key path so tests never touch the real `...\CurrentVersion\Run` key — they point at a throwaway subkey under `HKCU\Software\PowerGenieTests\<guid>` instead.

- [ ] **Step 1: Write the failing tests**

`tests/PowerGenie.Tests/AutoStartManagerTests.cs`:
```csharp
using Microsoft.Win32;
using PowerGenie.App.Services;

namespace PowerGenie.Tests;

public class AutoStartManagerTests : IDisposable
{
    private readonly string _testKeyPath = $@"Software\PowerGenieTests\{Guid.NewGuid()}";

    [Fact]
    public void SetEnabled_true_writes_a_registry_value_pointing_at_the_executable()
    {
        var manager = new AutoStartManager(_testKeyPath);

        manager.SetEnabled(true, @"C:\fake\PowerGenie.exe");

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void SetEnabled_false_removes_the_registry_value()
    {
        var manager = new AutoStartManager(_testKeyPath);
        manager.SetEnabled(true, @"C:\fake\PowerGenie.exe");

        manager.SetEnabled(false, @"C:\fake\PowerGenie.exe");

        Assert.False(manager.IsEnabled());
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_testKeyPath, throwOnMissingSubKey: false);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test PowerGenie.sln
```

Expected: FAIL — `AutoStartManager` does not exist.

- [ ] **Step 3: Implement `AutoStartManager`**

`src/PowerGenie.App/Services/AutoStartManager.cs`:
```csharp
using Microsoft.Win32;

namespace PowerGenie.App.Services;

public sealed class AutoStartManager
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PowerGenie";

    private readonly string _runKeyPath;

    public AutoStartManager(string? runKeyPath = null)
    {
        _runKeyPath = runKeyPath ?? DefaultRunKeyPath;
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test PowerGenie.sln
```

Expected: PASS — `AutoStartManagerTests` green.

- [ ] **Step 5: Commit**

```bash
git add src/PowerGenie.App/Services/AutoStartManager.cs tests/PowerGenie.Tests/AutoStartManagerTests.cs
git commit -m "Add AutoStartManager: toggle launch-at-login via HKCU Run key"
```

---

### Task 8: ProcessMonitorService

**Files:**
- Create: `src/PowerGenie.App/Services/ProcessMonitorService.cs`

**Interfaces:**
- Consumes: `PowerPlanService` (Task 5), `FileLogger` (Task 6), `RuleResolver.ResolveActivePlan` (Task 3), `AppConfig` (Task 2).
- Produces: `PowerGenie.App.Services.ProcessMonitorService` — `ProcessMonitorService(PowerPlanService, FileLogger, AppConfig initialConfig, TimeSpan pollInterval)`, `void UpdateConfig(AppConfig config)`, `void Start()`, `void Stop()`, `IDisposable`.

No automated test for this task: it wraps a real `System.Timers.Timer` and real `Process.GetProcesses()`, both already covered indirectly (the decision logic is `RuleResolver`, already tested in Task 3; the OS calls are already covered in Task 5). This task is verified manually in Task 10's end-to-end check.

- [ ] **Step 1: Implement `ProcessMonitorService`**

`src/PowerGenie.App/Services/ProcessMonitorService.cs`:
```csharp
using System.Diagnostics;
using PowerGenie.App.Models;

namespace PowerGenie.App.Services;

public sealed class ProcessMonitorService : IDisposable
{
    private readonly PowerPlanService _powerPlanService;
    private readonly FileLogger _logger;
    private readonly System.Timers.Timer _timer;

    private AppConfig _config;
    private readonly List<PowerPlan> _availablePlans;
    private Guid? _lastAppliedPlanGuid;

    public ProcessMonitorService(
        PowerPlanService powerPlanService,
        FileLogger logger,
        AppConfig initialConfig,
        TimeSpan pollInterval)
    {
        _powerPlanService = powerPlanService;
        _logger = logger;
        _config = initialConfig;
        _availablePlans = _powerPlanService.GetAvailablePlans();

        _timer = new System.Timers.Timer(pollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    public void UpdateConfig(AppConfig config) => _config = config;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void Tick()
    {
        try
        {
            var runningExeNames = Process.GetProcesses()
                .Select(TryGetExeName)
                .Where(name => name is not null)
                .Select(name => name!)
                .ToList();

            var availableGuids = new HashSet<Guid>(_availablePlans.Select(p => p.Guid));

            var resolvedPlanGuid = RuleResolver.ResolveActivePlan(
                runningExeNames,
                _config.Rules,
                availableGuids,
                _config.DefaultPlanGuid);

            if (resolvedPlanGuid != Guid.Empty && resolvedPlanGuid != _lastAppliedPlanGuid)
            {
                _powerPlanService.SetActivePlan(resolvedPlanGuid);
                _lastAppliedPlanGuid = resolvedPlanGuid;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Process monitor tick failed", ex);
        }
    }

    private static string? TryGetExeName(Process process)
    {
        try
        {
            return process.MainModule?.ModuleName;
        }
        catch
        {
            // Access denied on elevated/system processes is expected — skip them.
            return null;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build PowerGenie.sln
```

Expected: builds cleanly (no automated test added in this task; see Task 10 for the end-to-end manual verification that exercises this class).

- [ ] **Step 3: Commit**

```bash
git add src/PowerGenie.App/Services/ProcessMonitorService.cs
git commit -m "Add ProcessMonitorService: poll processes and switch plans on change"
```

---

### Task 9: Settings window + Add Rule dialog

**Files:**
- Create: `src/PowerGenie.App/ViewModels/RuleRow.cs`
- Create: `src/PowerGenie.App/Views/SettingsWindow.xaml`
- Create: `src/PowerGenie.App/Views/SettingsWindow.xaml.cs`
- Create: `src/PowerGenie.App/Views/AddRuleDialog.xaml`
- Create: `src/PowerGenie.App/Views/AddRuleDialog.xaml.cs`

**Interfaces:**
- Consumes: `ConfigStore`, `PowerPlanService`, `AutoStartManager` (Tasks 4, 5, 7), `AppConfig`, `AppRule`, `PowerPlan` (Task 2).
- Produces: `PowerGenie.App.Views.SettingsWindow` — `SettingsWindow(ConfigStore, PowerPlanService, AutoStartManager)`, `event Action<AppConfig>? ConfigSaved`.
- Produces: `PowerGenie.App.Views.AddRuleDialog` — `AddRuleDialog(List<PowerPlan> availablePlans)`, `AppRule? CreatedRule` (set only when the dialog closes with `DialogResult == true`).

No automated tests: these are WPF windows (file-picker dialogs, live process lists) — verified manually in Step 5 below and again end-to-end in Task 10.

- [ ] **Step 1: Create `src/PowerGenie.App/ViewModels/RuleRow.cs`**

```csharp
namespace PowerGenie.App.ViewModels;

public sealed class RuleRow
{
    public required string ExeName { get; init; }
    public required string DisplayName { get; init; }
    public required Guid PlanGuid { get; init; }
    public required string PlanName { get; init; }
}
```

- [ ] **Step 2: Create `src/PowerGenie.App/Views/SettingsWindow.xaml`**

```xml
<Window x:Class="PowerGenie.App.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Power Genie Settings" Height="420" Width="520"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="0,0,0,12">
            <TextBlock Text="Default power plan:" VerticalAlignment="Center" Margin="0,0,8,0"/>
            <ComboBox x:Name="DefaultPlanComboBox" Width="260" DisplayMemberPath="Name"/>
        </StackPanel>

        <DataGrid x:Name="RulesGrid" Grid.Row="1" AutoGenerateColumns="False"
                  CanUserAddRows="False" IsReadOnly="True">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Application" Binding="{Binding DisplayName}" Width="*"/>
                <DataGridTextColumn Header="Power Plan" Binding="{Binding PlanName}" Width="*"/>
            </DataGrid.Columns>
        </DataGrid>

        <StackPanel Orientation="Horizontal" Grid.Row="2" Margin="0,8,0,0">
            <Button x:Name="AddRuleButton" Content="Add rule" Width="90" Margin="0,0,8,0" Click="AddRuleButton_Click"/>
            <Button x:Name="RemoveRuleButton" Content="Remove rule" Width="90" Click="RemoveRuleButton_Click"/>
        </StackPanel>

        <StackPanel Orientation="Horizontal" Grid.Row="3" Margin="0,16,0,0" HorizontalAlignment="Right">
            <CheckBox x:Name="StartWithWindowsCheckBox" Content="Start with Windows" VerticalAlignment="Center" Margin="0,0,16,0"/>
            <Button x:Name="SaveButton" Content="Save" Width="90" Click="SaveButton_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Create `src/PowerGenie.App/Views/SettingsWindow.xaml.cs`**

```csharp
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using PowerGenie.App.Models;
using PowerGenie.App.Services;
using PowerGenie.App.ViewModels;

namespace PowerGenie.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigStore _configStore;
    private readonly PowerPlanService _powerPlanService;
    private readonly AutoStartManager _autoStartManager;
    private readonly ObservableCollection<RuleRow> _ruleRows = new();

    private List<PowerPlan> _availablePlans = new();
    private AppConfig _config = new();

    public event Action<AppConfig>? ConfigSaved;

    public SettingsWindow(ConfigStore configStore, PowerPlanService powerPlanService, AutoStartManager autoStartManager)
    {
        InitializeComponent();
        _configStore = configStore;
        _powerPlanService = powerPlanService;
        _autoStartManager = autoStartManager;

        RulesGrid.ItemsSource = _ruleRows;
        LoadState();
    }

    private void LoadState()
    {
        _availablePlans = _powerPlanService.GetAvailablePlans();
        _config = _configStore.Load();

        DefaultPlanComboBox.ItemsSource = _availablePlans;
        DefaultPlanComboBox.SelectedItem = _availablePlans.FirstOrDefault(p => p.Guid == _config.DefaultPlanGuid)
            ?? _availablePlans.FirstOrDefault();

        StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;

        RefreshRuleRows();
    }

    private void RefreshRuleRows()
    {
        _ruleRows.Clear();
        foreach (var rule in _config.Rules)
        {
            var planName = _availablePlans.FirstOrDefault(p => p.Guid == rule.PlanGuid)?.Name ?? "(deleted plan)";
            _ruleRows.Add(new RuleRow
            {
                ExeName = rule.ExeName,
                DisplayName = rule.DisplayName,
                PlanGuid = rule.PlanGuid,
                PlanName = planName
            });
        }
    }

    private void AddRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddRuleDialog(_availablePlans) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedRule is not null)
        {
            _config.Rules.Add(dialog.CreatedRule);
            RefreshRuleRows();
        }
    }

    private void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is RuleRow selected)
        {
            _config.Rules.RemoveAll(r => r.ExeName == selected.ExeName && r.PlanGuid == selected.PlanGuid);
            RefreshRuleRows();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DefaultPlanComboBox.SelectedItem is PowerPlan selectedDefault)
        {
            _config.DefaultPlanGuid = selectedDefault.Guid;
        }

        _config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _configStore.Save(_config);

        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        _autoStartManager.SetEnabled(_config.StartWithWindows, exePath);

        ConfigSaved?.Invoke(_config);
        Close();
    }
}
```

- [ ] **Step 4: Create `src/PowerGenie.App/Views/AddRuleDialog.xaml`**

```xml
<Window x:Class="PowerGenie.App.Views.AddRuleDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Add rule" Height="240" Width="420"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="0,0,0,8">
            <Button x:Name="BrowseButton" Content="Browse for .exe..." Width="140" Click="BrowseButton_Click"/>
            <TextBlock Text="or pick a running process:" VerticalAlignment="Center" Margin="12,0,0,0"/>
        </StackPanel>

        <ComboBox x:Name="RunningProcessComboBox" Grid.Row="1" Margin="0,0,0,8"
                  DisplayMemberPath="DisplayName" SelectionChanged="RunningProcessComboBox_SelectionChanged"/>

        <TextBlock x:Name="SelectedExeText" Grid.Row="2" Margin="0,0,0,8" TextWrapping="Wrap"/>

        <StackPanel Orientation="Horizontal" Grid.Row="3" Margin="0,0,0,8">
            <TextBlock Text="Display name:" VerticalAlignment="Center" Margin="0,0,8,0"/>
            <TextBox x:Name="DisplayNameTextBox" Width="220" TextChanged="DisplayNameTextBox_TextChanged"/>
        </StackPanel>

        <StackPanel Orientation="Horizontal" Grid.Row="4" VerticalAlignment="Top">
            <TextBlock Text="Power plan:" VerticalAlignment="Center" Margin="0,0,8,0"/>
            <ComboBox x:Name="PlanComboBox" Width="220" DisplayMemberPath="Name" SelectionChanged="PlanComboBox_SelectionChanged"/>
        </StackPanel>

        <StackPanel Orientation="Horizontal" Grid.Row="5" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="Cancel" Width="80" Margin="0,0,8,0" Click="CancelButton_Click"/>
            <Button x:Name="OkButton" Content="OK" Width="80" IsEnabled="False" Click="OkButton_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 5: Create `src/PowerGenie.App/Views/AddRuleDialog.xaml.cs`**

```csharp
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PowerGenie.App.Models;

namespace PowerGenie.App.Views;

public partial class AddRuleDialog : Window
{
    private sealed record RunningProcessOption(string DisplayName, string ExeName);

    private readonly List<PowerPlan> _availablePlans;
    private string? _selectedExeName;

    public AppRule? CreatedRule { get; private set; }

    public AddRuleDialog(List<PowerPlan> availablePlans)
    {
        InitializeComponent();
        _availablePlans = availablePlans;
        PlanComboBox.ItemsSource = _availablePlans;

        RunningProcessComboBox.ItemsSource = Process.GetProcesses()
            .Select(TryDescribe)
            .Where(option => option is not null)
            .Select(option => option!)
            .DistinctBy(option => option.ExeName)
            .OrderBy(option => option.DisplayName)
            .ToList();
    }

    private static RunningProcessOption? TryDescribe(Process process)
    {
        try
        {
            var exeName = process.MainModule?.ModuleName;
            return exeName is null ? null : new RunningProcessOption($"{process.ProcessName} ({exeName})", exeName);
        }
        catch
        {
            return null;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            Title = "Select application"
        };

        if (dialog.ShowDialog() == true)
        {
            SetSelectedExe(Path.GetFileName(dialog.FileName));
        }
    }

    private void RunningProcessComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RunningProcessComboBox.SelectedItem is RunningProcessOption option)
        {
            SetSelectedExe(option.ExeName);
        }
    }

    private void SetSelectedExe(string exeName)
    {
        _selectedExeName = exeName;
        SelectedExeText.Text = $"Selected: {exeName}";
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            DisplayNameTextBox.Text = Path.GetFileNameWithoutExtension(exeName);
        }

        UpdateOkButtonState();
    }

    private void PlanComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateOkButtonState();

    private void DisplayNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => UpdateOkButtonState();

    private void UpdateOkButtonState()
    {
        OkButton.IsEnabled = _selectedExeName is not null
            && !string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
            && PlanComboBox.SelectedItem is not null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedExeName is null || PlanComboBox.SelectedItem is not PowerPlan selectedPlan)
        {
            return;
        }

        CreatedRule = new AppRule
        {
            ExeName = _selectedExeName,
            DisplayName = DisplayNameTextBox.Text,
            PlanGuid = selectedPlan.Guid
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
```

- [ ] **Step 6: Build**

```bash
dotnet build PowerGenie.sln
```

Expected: builds cleanly.

- [ ] **Step 7: Manual check**

Temporarily change `App.xaml.cs`'s `TrayIconManager(() => { })` call to `TrayIconManager(() => new Views.SettingsWindow(new Services.ConfigStore(Services.ConfigStore.GetDefaultFilePath()), new Services.PowerPlanService(), new Services.AutoStartManager()).Show())`, run the app, right-click the tray icon → Settings. Confirm: the Default plan dropdown lists your real plans (Balanced, Power saver, High performance, etc.), Add rule → Browse works, Add rule → pick a running process works, Remove rule works, Save writes `%AppData%\PowerGenie\config.json`. Revert the temporary `App.xaml.cs` change — full wiring lands in Task 10.

- [ ] **Step 8: Commit**

```bash
git add src/PowerGenie.App/ViewModels src/PowerGenie.App/Views
git commit -m "Add Settings window and Add Rule dialog"
```

---

### Task 10: Full app wiring + end-to-end verification

**Files:**
- Modify: `src/PowerGenie.App/TrayIconManager.cs` (no change needed — already takes the callback since Task 1)
- Modify: `src/PowerGenie.App/App.xaml.cs`
- Modify: `README.md`
- Modify: `docs/design.md` (flip status line)

**Interfaces:**
- Consumes everything from Tasks 1–9.

- [ ] **Step 1: Replace `src/PowerGenie.App/App.xaml.cs`**

```csharp
using System.Windows;
using PowerGenie.App.Services;
using PowerGenie.App.Views;

namespace PowerGenie.App;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;
    private ProcessMonitorService? _monitor;
    private ConfigStore? _configStore;
    private PowerPlanService? _powerPlanService;
    private AutoStartManager? _autoStartManager;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _configStore = new ConfigStore(ConfigStore.GetDefaultFilePath());
        _powerPlanService = new PowerPlanService();
        _autoStartManager = new AutoStartManager();
        var logger = new FileLogger(FileLogger.GetDefaultFilePath());

        var config = _configStore.Load();

        _monitor = new ProcessMonitorService(_powerPlanService, logger, config, TimeSpan.FromSeconds(3));
        _monitor.Start();

        _trayIconManager = new TrayIconManager(OpenSettings);
        _trayIconManager.Show();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_configStore!, _powerPlanService!, _autoStartManager!);
        _settingsWindow.ConfigSaved += config => _monitor!.UpdateConfig(config);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Stop();
        _monitor?.Dispose();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 2: Build and run the full test suite**

```bash
dotnet build PowerGenie.sln
dotnet test PowerGenie.sln
```

Expected: build succeeds, every test from Tasks 1–7 still passes.

- [ ] **Step 3: End-to-end manual verification**

```bash
dotnet run --project src/PowerGenie.App
```

1. Confirm the tray icon appears with no visible window.
2. Right-click → Settings. Set Default plan to **Power saver**. Add a rule: `notepad.exe` → **High performance** (pick it via "running process" after launching Notepad, or via Browse to `C:\Windows\System32\notepad.exe`). Check "Start with Windows". Save.
3. In a separate terminal, run `powercfg /list` — confirm Power saver is `*` active (since Notepad may already be open from step 2 picking, close it first if so).
4. Launch `notepad.exe`. Wait ~3–5 seconds. Run `powercfg /list` again — confirm High performance is now `*` active.
5. Close Notepad. Wait ~3–5 seconds. Run `powercfg /list` again — confirm it reverted to Power saver.
6. Run `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v PowerGenie` — confirm the value exists and points at the built exe.
7. Reopen Settings, uncheck "Start with Windows", Save. Re-run the same `reg query` — confirm the value is gone.
8. Right-click the tray icon → Exit. Confirm the process ends (check Task Manager) and the tray icon disappears.
9. Manually restore your normal active plan if the above left it somewhere unexpected: `powercfg /setactive <your-usual-plan-guid>`.

- [ ] **Step 4: Update `README.md`**

Replace the "Planned features" section with:

```markdown
## Features

- Pick a **default power plan** from every scheme available on your PC.
- Define **per-app rules**: when app X is running, switch to plan Y.
- Add an app by browsing to its `.exe` or picking it from currently running processes.
- Lightweight background monitoring (polls every 3 seconds), automatic switch-back to the default plan when no rule matches.
- Optional start with Windows.

## Building and running

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build PowerGenie.sln
dotnet run --project src/PowerGenie.App
```

## Running the tests

```bash
dotnet test PowerGenie.sln
```
```

Also change the "Status" line near the top from "In active development. Design in progress" to "v1 implemented — see Features below."

- [ ] **Step 5: Update `docs/design.md`**

Change the `Status: draft, pending review.` line at the top to `Status: implemented (v1).`

- [ ] **Step 6: Commit and push**

```bash
git add src/PowerGenie.App/App.xaml.cs README.md docs/design.md
git commit -m "Wire tray icon, settings, and process monitor into a full app; v1 complete"
git push
```
