# Power Genie

A small Windows tray utility that automatically switches your active power plan based on what's running.

Keep your PC on **Power saver** most of the time, and let Power Genie switch to **High performance** (or any plan you choose) the moment an app you care about — a slicer, a game, a render job — starts, then switch back when it closes.

## Status

✅ v1 implemented — see Features below.

## Features

- Pick a **default power plan** from every scheme available on your PC.
- Define **per-app rules**: when app X is running, switch to plan Y.
- Add an app by browsing to its `.exe` or picking it from currently running processes.
- Lightweight background monitoring (polls every 3 seconds), automatic switch-back to the default plan when no rule matches.
- Optional start with Windows.

## Get a standalone PowerGenie.exe

No install, no .NET runtime required — build a single self-contained executable and put it wherever you like (a Programs folder, the Desktop, wherever). Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) once, only to build it:

```bash
dotnet publish src/PowerGenie.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o publish
```

This produces `publish/PowerGenie.exe` (~160 MB, since it bundles its own .NET runtime). Move that one file anywhere and double-click it — no terminal needed after that. Add a shortcut to it in your Windows Startup folder, or just check "Start with Windows" in the app's Settings.

## Building and running from source (for development)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build PowerGenie.sln
dotnet run --project src/PowerGenie.App
```

## Running the tests

```bash
dotnet test PowerGenie.sln
```

## License

MIT — see [LICENSE](LICENSE).
