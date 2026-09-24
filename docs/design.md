# Power Genie — Design

Status: draft, pending review.

## Problem / intent

The user runs Windows on **Power saver** most of the time (quiet, low power draw), but
certain apps (e.g. Bambu Studio, slicers, games, render jobs) benefit from a
**High performance** plan. Manually switching plans is easy to forget. Power Genie
watches for chosen apps and switches the active Windows power plan automatically,
reverting when they close.

Audience: a single user running it on their own PC, released as an open-source
utility others with the same itch can use.

## Approach

A single **C#/.NET WPF** application, distributed as a tray-only app (no visible
main window on launch).

### Components

1. **Tray icon** (`NotifyIcon`) — right-click menu: *Settings*, *Exit*. No main
   window shown on startup.
2. **Settings window**
   - **Default plan** dropdown — populated from every scheme returned by
     `powercfg /list`. Used whenever no rule matches a running process.
   - **Rules table** — rows of `App -> Power Plan`. Buttons: *Add rule*, *Remove
     rule*.
     - *Add rule* opens a small dialog with two ways to pick the app:
       - **Browse for .exe** (standard `OpenFileDialog`).
       - **Pick from a running process** (dropdown built from
         `Process.GetProcesses()`, deduplicated by main module path).
     - A plan dropdown (same source as Default plan) picks the target scheme.
   - **Start with Windows** checkbox, defaults to checked.
   - *Save* persists the model to `%AppData%\PowerGenie\config.json`.
3. **Background monitor** — a timer polls the running-process list every 2–3
   seconds:
   - Build the set of currently running executable names.
   - Walk the rule list in order; the **first rule whose exe is running wins**
     (simple, predictable tie-break when multiple tracked apps run at once).
   - If no rule matches, the **default plan** applies.
   - Only calls `powercfg /setactive <GUID>` when the resolved plan differs
     from the last-applied one — not every poll tick.
4. **Autostart** — toggling "Start with Windows" writes/removes a value under
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the app's
   exe. No admin rights required (HKCU, not HKLM).

### Data flow

Settings UI edits an in-memory model (shared singleton) → *Save* serializes it
to `config.json` → the monitor loop reads the same in-memory model, so rule
changes take effect immediately without restarting the app. On launch, the
app deserializes `config.json` into that model (or starts with an empty rule
set + "Balanced" as default if no config exists yet).

### Error handling

- A saved rule whose plan GUID no longer exists (e.g. the user deleted a
  custom scheme) is skipped by the monitor and flagged in the rules table
  (e.g. a warning icon), not treated as fatal.
- A failed `powercfg` call (bad GUID, unexpected exit code) is caught and
  written to a small rolling log file (`%AppData%\PowerGenie\log.txt`); the
  monitor loop keeps running.
- Adding a rule via "Browse for .exe" validates the file exists before it's
  accepted.

### Testing

- The core decision — *"given this rule list and this set of running
  processes, which plan should be active?"* — is a small pure function,
  independent of the UI, timer, and `powercfg`. It gets unit tests (xUnit)
  covering: no rules, one match, multiple simultaneous matches (tie-break
  order), and a rule whose plan no longer exists.
- Tray icon, settings UI, autostart registry write, and actual `powercfg`
  switching are verified manually (launch test processes such as
  `notepad.exe`, confirm the plan switches within the poll interval and
  reverts on close).

## Out of scope (for v1)

- Searching installed programs via Start Menu shortcuts / registry uninstall
  entries (more parsing work for marginal benefit over browse + running-process
  picker).
- WMI event-driven detection (polling is simpler, doesn't need elevated
  permissions, and 2–3s latency is fine for this use case).
- Per-user vs. per-machine install, code signing, auto-update — not needed for
  a personal open-source tool at this stage.

## Open questions

None blocking; polling interval (2–3s), tie-break rule (first match in list
order), and whether Settings should show the *currently active* plan live are
all resolved as above but easy to revisit after first use.
