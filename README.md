# Power Genie

A Windows tray utility that automatically switches your active power plan based on what's running.

[![Release](https://img.shields.io/github/v/release/MarcelodeFreitas/power-genie)](https://github.com/MarcelodeFreitas/power-genie/releases/latest)
[![License](https://img.shields.io/github/license/MarcelodeFreitas/power-genie)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows-blue)

## Why

Power Genie watches for the apps you actually care about and switches to whatever plan makes sense the moment they start, then switches back the moment they close. Power saver as the default is just what fits my own use case. The tool doesn't care what you pick: if your day-to-day needs more headroom, set your default to Balanced or High performance instead and use a rule to drop to Power saver for the one app where you want it quiet. Set it up once for however you actually work, and forget about it.

## Features

| Feature | Description |
| --- | --- |
| Per-app rules | Map any app to a specific power plan (e.g. Bambu Studio to Ultimate Performance). Add, edit, or remove rules, saved instantly. |
| Default plan | Pick the plan used whenever no rule matches, from every scheme available on your PC. |
| Three ways to find an app | Search your installed programs, search currently running processes, or point at a `.exe` path directly. |
| Live tray icon | Shows a colored dot for whichever plan is currently active, with a tooltip naming it. Assign your own color per plan in Settings. |
| Background monitoring | Polls every 3 seconds and switches automatically, no manual toggling. |
| Start with Windows | Optional, one checkbox. |

## Download

Grab `PowerGenie.exe` from the [latest release](https://github.com/MarcelodeFreitas/power-genie/releases/latest). It's a single self-contained file (about 160 MB, since it bundles its own .NET runtime), so there's no install and no .NET runtime required on your machine.

1. Download `PowerGenie.exe` and put it wherever you like (a Programs folder, the Desktop, wherever).
2. Double-click it. A tray icon appears.
3. Right-click the tray icon, choose **Settings**, pick a default plan, and add a rule for an app you care about.
4. Check **Start with Windows** if you want it to launch automatically.

## Building it yourself

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0):

```bash
dotnet publish src/PowerGenie.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o publish
```

This produces `publish/PowerGenie.exe`, built the same way as the file on the Releases page.

### Running from source (for development)

```bash
dotnet build PowerGenie.sln
dotnet run --project src/PowerGenie.App
```

### Running the tests

```bash
dotnet test PowerGenie.sln
```

## Contributing

This started as a personal tool, but issues and pull requests are welcome if you find a bug or want to suggest something.

## License

MIT, see [LICENSE](LICENSE).
