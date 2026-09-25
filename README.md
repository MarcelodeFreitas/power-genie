# Power Genie

A small Windows tray utility that automatically switches your active power plan based on what's running.

Keep your PC on **Power saver** most of the time, and let Power Genie switch to **High performance** (or any plan you choose) the moment an app you care about — a slicer, a game, a render job — starts, then switch back when it closes.

## Status

✅ v1.0.0 released — see [Releases](../../releases) for the latest download.

## Features

- Pick a **default power plan** from every scheme available on your PC.
- Define **per-app rules**: when app X is running, switch to plan Y — add, edit, or remove rules, saved instantly.
- Find an app to add three ways: a searchable list of your **installed programs**, a searchable list of **currently running processes**, or a direct `.exe` path (browse or type/paste one in).
- The tray icon shows a **colored dot for whichever plan is currently active**, with a tooltip naming it — assign your own color to each plan in Settings, or let it auto-assign one.
- Lightweight background monitoring (polls every 3 seconds), automatic switch-back to the default plan when no rule matches.
- Optional start with Windows.

## Download

Grab the latest `PowerGenie.exe` from the [Releases page](../../releases/latest). It's a single self-contained file (~160 MB, since it bundles its own .NET runtime) — no install, no .NET runtime required on your machine. Move it wherever you like (a Programs folder, the Desktop, wherever) and double-click it. Add a shortcut to it in your Windows Startup folder, or just check "Start with Windows" in the app's own Settings.

## Building it yourself

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), only to build it:

```bash
dotnet publish src/PowerGenie.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o publish
```

This produces `publish/PowerGenie.exe`, identical in kind to what's on the Releases page.

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
