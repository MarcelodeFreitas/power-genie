# Power Genie

A small Windows tray utility that automatically switches your active power plan based on what's running.

Keep your PC on **Power saver** most of the time, and let Power Genie switch to **High performance** (or any plan you choose) the moment an app you care about — a slicer, a game, a render job — starts, then switch back when it closes.

## Status

🚧 In active development. Design in progress — see [`docs/design.md`](docs/design.md).

## Planned features

- Pick a **default power plan** from every scheme available on your PC.
- Define **per-app rules**: when app X is running, switch to plan Y.
- Add an app by browsing to its `.exe` or picking it from currently running processes.
- Lightweight background monitoring (polling), automatic switch-back to the default plan when no rule matches.
- Optional start with Windows.

## License

MIT — see [LICENSE](LICENSE).
