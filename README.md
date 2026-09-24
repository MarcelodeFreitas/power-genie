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

## License

MIT — see [LICENSE](LICENSE).
