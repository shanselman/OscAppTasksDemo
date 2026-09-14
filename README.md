# OSC to Windows Shell App Tasks

A local, presentation-ready proof of this pipeline:

```text
Fake C# agent -> redirected stdout bytes -> incremental OSC parser
             -> one-command lifecycle -> real AppTaskInfo -> Windows Shell
```

The packaged WinUI host shows **process output**, **decoded OSC events**, and
**actual Shell API results** side by side. A separately labeled **local preview**
shows the interpreted state even when Shell support is unavailable.

**This is a process-output demo, not a terminal emulator.** It does not use
ConPTY, intercept another application's Windows Terminal output, or run an
interactive shell. The fake agent makes no AI/network calls and modifies no user
files. The host does create persistent, real Windows Shell tasks.

For the implemented compatible activity/step tier and the separately proposed
structured extension, see [Rich OSC task cards](docs/rich-osc-task-cards.md).
For commit-pinned upstream attachment points and obstacles, see
[intelligent-terminal integration findings](docs/intelligent-terminal-integration.md).

## Quick start

From this repository in PowerShell:

```powershell
.\Demo.ps1
```

This publishes the agent, builds the host, registers a development package with
`winapp`, and launches it with package identity and debug-output capture. The
command stays attached until the app closes. Choose a scenario, then **Run / replay**.
The fixed fake agent's OSC 2 titles are published as activity labels. The visible
**OPT-IN** checkbox is off by default: enable it before running only if you want
title changes to imply completion of the preceding activity. This is an agreed
demo convention, not standard OSC semantics.

```powershell
.\Demo.ps1 -Test         # Standalone parser/lifecycle/real-subprocess tests
.\Demo.ps1 -BuildOnly    # Build only; does NOT refresh an installed package
.\Demo.ps1 -Platform ARM64   # ARM64 build/run option; not validated on ARM64
```

Close the existing host before a normal rebuild/relaunch. `-BuildOnly` leaves a
running app and its registered layout untouched; run `.\Demo.ps1` to deploy new
binaries. Never run `OscTasks.Host.exe` directly, remove the manifest, or switch
the host to unpackaged mode.

To show the independent CLI in your own terminal:

```powershell
dotnet run --project .\OscTasks.Agent -- success
dotnet run --project .\OscTasks.Agent -- failure
dotnet run --project .\OscTasks.Agent -- indeterminate
```

That terminal may render OSC title/progress itself, but these standalone commands
do **not** create Shell tasks: the CLI has no Windows/WinRT dependencies. Only the
packaged demo host bridges the captured stream to `AppTaskInfo`.

## Requirements

| Requirement | Details |
|---|---|
| Windows | Windows 11 with the experimental App Tasks rollout enabled for real cards; OS build number alone is not a support guarantee. |
| .NET | .NET 10 SDK; projects target `net10.0`. Agent deployment is framework-dependent. |
| Windows projection | `WindowsSdkPackageVersion` **10.0.26100.87**, explicitly pinned in Host and Shell; this projection includes `Windows.UI.Shell.Tasks`. Older projections may not. |
| Windows App SDK | **2.4.0**, with Windows SDK BuildTools **10.0.28000.2705** and BuildTools.WinApp **0.6.1**, restored by the build. |
| WinApp CLI | Installed `winapp` **>= 0.3**, current version recommended; **0.6.0** was used here. |
| Development packaging | Developer Mode enabled and real package identity; the manifest declares the `com.microsoft.apptaskprovider` extension and a `Public` folder. |
| Architecture | Debug x64 is the verified configuration. ARM64 is an unverified script option; Release/trimming is not validated. |

For missing tooling, use `/winui-setup` in a WinUI-enabled Copilot environment.
The demo script does not install prerequisites or change system settings.
The WinUI project was scaffolded with `dotnet new winui-mvvm`; running the existing
source does not require regenerating the template.

If an old developer alias shadows the installed current CLI, select the installed
current executable through your **process-local PATH** before running the script.
For example, if the official package-specific alias exists:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\WindowsApps\winapp_8wekyb3d8bbwe;$env:PATH"
winapp --version
.\Demo.ps1
```

No global alias changes or uninstallations are needed.

## Three-minute presentation

1. Launch `.\Demo.ps1`. Explain the three panes and the support banner. The left
   pane is real captured stdout; the middle pane is decoded control traffic;
   the right pane contains real API return/readback information, **not** a
   painted imitation of the Shell.
2. Select **success**, optionally enable **OPT-IN** title steps, and click
   **Run / replay**. Activity titles change from Inspecting project to Editing
   files to Running tests; numeric progress moves through 10%, 45%, 65%, and 100%.
   A repeated Running tests title adds no extra step. Point out that the task
   stays running at 100% and after clear. Only `OSC 133;D;0` authorizes success.
   With the checkbox off, activity/progress still appear, but there is no inferred
   completed-step history. With it on, the bridge sends actual completed/executing
   steps through `AppTaskContent.CreateSequenceOfSteps`.
3. Open the demo's taskbar task flyout. On supported Windows, inspect the genuine
   Completed card. Click **Show details** to activate the existing demo window
   and inspect the matching persisted task.
4. Select **failure**, then **Run / replay**. Red progress is still running;
   `OSC 133;D;1` produces Error. The earlier success card remains available
   alongside the failure card.
5. Optionally show **indeterminate** or **warning**, or cancel a running scenario.
   Explain that progress styling is not a request for input.
6. **Reset view** clears only the host's current logs and local preview.
   **Clear demo tasks** explicitly removes this provider's demo tasks, including
   tasks from previous runs. Do not clear cards you still want to present.

The agent pauses about 1.6 seconds per stage. `--fast` is available for automated
CLI tests, not used by the presentation host.

## Scenarios and mapping

| Scenario | Meaning | Local final state | Shell final state |
|---|---|---|---|
| `success` | 100%, clear, then explicit exit 0 | Completed | Completed |
| `failure` | Error-colored progress, then explicit exit 1 | Error | Error |
| `indeterminate` | Indeterminate interval, then explicit exit 0 | Completed | Completed |
| `warning` | Warning-colored progress; no input request | Completed | Completed |
| `unknown` | Finish marker with no exit code, process exits 0 | Unknown | Error, with unknown-outcome explanation |
| `crash` | Simulated abrupt nonzero exit 7 without finish marker | Error | Error |
| Cancel button | Host kills/reaps its child; work is not resumable | Cancelled | Error, with cancellation explanation |

The `crash` scenario simulates an unexpected process exit; it is not a native
crash or crash-dump test. Unknown and Cancelled use Shell **Error** as an explicit
demo policy because AppTaskState has no equivalent states. They are not falsely
represented as successful, Paused, or NeedsAttention.

| Wire sequence (BEL or ESC-backslash terminated) | Interpretation |
|---|---|
| `OSC 0;<title>` / `OSC 2;<title>` | Standard title metadata. The fixed demo explicitly treats in-command titles as activity; optional step inference is a separate convention. |
| `OSC 133;A` | Prompt begins. |
| `OSC 133;B` | Command input begins; **not execution**. |
| `OSC 133;C` | Execution/output begins; transition to Running. |
| `OSC 133;D;0` | Completed. |
| `OSC 133;D;<nonzero>` | Error. |
| `OSC 133;D` | Ended, but outcome is Unknown. |
| `OSC 9;4;0[;percent]` | Clear/hide progress; **not completion**. |
| `OSC 9;4;1;<percent>` | Numeric progress 0..100; **100 is not completion**. |
| `OSC 9;4;2;<percent>` | Error-colored progress; **not terminal failure**. |
| `OSC 9;4;3[;percent]` | Indeterminate progress. |
| `OSC 9;4;4;<percent>` | Warning-colored progress; **not Paused or NeedsAttention**. |

The fake CLI emits **synthetic shell lifecycle markers** around one invocation;
a real shell normally emits OSC 133. No custom OSC protocol or ConEmu-specific
`OSC 9;3` title extension is required. Numeric states require a percentage in this
demo's supported subset. Other OSC/shell-integration extensions are ignored or
reported as malformed rather than inferred.

There is no documented numeric-percent property on `AppTaskInfo`.
Running content uses `AppTaskContent.CreateSequenceOfSteps` with a textual progress
label and, when opted in, completed activities. Terminal content uses
`CreateTextSummaryResult`: success completes the last opt-in activity; failure,
unknown outcome, and cancellation retain it as **unfinished**, never completed.

### Activity convention

The reusable lifecycle defaults both title publication and step inference **off**
for arbitrary sources. The fixed demo host explicitly enables title-as-activity
and visibly discloses that these labels go to Shell. The checkbox separately
enables title-to-step history for the next run and is locked during that run.

Only titles received after `OSC 133;C` can be activity transitions; pre-command
branding/cwd titles remain metadata. Consecutive identical titles and empty titles
do not complete a step. A later return to an earlier title is a new activity, not
global deduplication. Progress is invocation-level, so title changes preserve its
latest value; it is not assumed to be a per-step percentage. The producer promises
that each different nonempty in-command title means its previous activity finished.

History is immutable per snapshot and retains the latest eight completed activities,
with an explicit omitted count. Activity strings are sanitized/capped at 120
characters. Reset/replay creates a fresh lifecycle with no stale history, title,
progress, or inferred success. Ordinary stdout never becomes steps or summaries.

## Structure and reuse

| Project | Responsibility |
|---|---|
| `OscTasks.Agent` | Independent C# console simulation; emits narrative and OSC bytes, knows nothing about Shell APIs. |
| `OscTasks.Core` | Platform-neutral `OscParser`, immutable snapshots/`TaskLifecycle`, and `AgentProcess` byte-stream transport. |
| `OscTasks.Shell` | Packaged WinRT boundary: support probe, real Create/Update/FindAll/Remove, ownership and activation-route filtering. No WinUI dependency. |
| `OscTasks.Host` | WinUI presentation and orchestration; fixed bundled agent, cancellation, throttled UI/API updates, single-instance protocol activation. |
| `OscTasks.Tests` | Dependency-free executable test harness using actual agent subprocesses; no Shell task creation. |

For a later `microsoft/intelligent-terminal` integration, reuse the parser,
lifecycle, and Shell adapter rather than this UI. Feed bytes from that host's
transport and maintain one lifecycle per invocation. A real terminal integration
would need its own session/command correlation, ConPTY pass-through verification,
trust boundaries, and lifecycle policy. No fork or integration is included here.

## Safety, persistence, and limits

- UTF-8 decoding and OSC parsing are incremental across arbitrary reads, including
  split multibyte characters and terminators. Malformed UTF-8 is replaced and
  reported. Oversized/malformed OSC payloads are discarded until a terminator;
  EOF reports truncation. A valid literal U+FFFD is not mistaken for a decode error.
- OSC payloads are bounded at 4096 characters. Text emission is chunked; the host
  retains at most 32768 output characters and 120 decoded-event entries, with
  visible trimming notices. UI/Shell updates are throttled to four per second
  during a run, plus an immediate final refresh. Intermediate states may coalesce.
- This is one command per run, not a multi-command shell. Once a terminal outcome
  is accepted, later markers/disconnects cannot produce another terminal transition.
  A missing finish marker plus process exit 0 is **Unknown**, not success.
- Output is data, never input. The host uses `ProcessStartInfo.ArgumentList` with
  a fixed bundled executable, no shell, and no output-triggered command/URI execution.
  Stderr is drained separately and is not interpreted as OSC.
- Host-generated scenario/state labels and the fixed fake agent's sanitized
  activity titles go to Shell. Stdout narrative, stderr, and command lines do not.
  For arbitrary sources, title publication requires a separate explicit lifecycle
  opt-in; title-to-step inference requires another. This is privacy minimization,
  not a security boundary against a malicious child falsifying titles/markers.
- Cancellation and normal window close are wired to stop/reap the owned child
  process tree. Hard termination of the host itself is not covered by a Job Object;
  recovery from host/OS crashes is outside this demo.
- Tasks persist across app sessions/reboots. Startup reports previous demo tasks;
  it does not pretend stale Running tasks are live, resume them, or auto-delete them.
  Replay deliberately creates a new task.
- Before updating an existing task, the adapter checks `FindAll` and `HiddenByUser`.
  Hidden/removed tasks are not recreated in that run. Cleanup uses current-app
  enumeration plus the demo group title and safe route, never another provider's data.
- The only accepted activation route is `osctasksdemo://task/<GUID>` with no query,
  fragment, credentials, or port. It opens/inspects the demo task; it never executes
  commands. A Shell card can reactivate the existing app or start the packaged app.
- Missing runtime types or `IsSupported() == false` produce a prominent
  **Unsupported / local preview only** banner. API failures show the operation,
  exception, HRESULT, and message. They stop publishing until an explicit new run;
  no fake card substitutes for success.
- An unexpected null `FindAll` is reported as unavailable enumeration, **not**
  silently treated as an empty collection. Startup can still attempt Create on
  an explicit run. Null enumeration while updating blocks updates because hidden
  state cannot be checked. Clear/activation also report null explicitly.
- Null content/Create results are failures. A successful API result includes
  task ID, state readback, subtitle, and deep link. **API success alone does not
  prove Shell visibility**; inspect the actual Shell separately.

## Verification and known gaps

Observed on **2026-09-14**, Windows **26H2 build 26340.9233**, Debug x64:

- Packaged host built and launched with WinApp 0.6.0. Registered manifest contained
  the App Task provider extension, protocol registration, and bundled agent.
- `AppTaskInfo.IsSupported()` returned true. Real Create/Update worked; completed
  state and a genuine task ID were read back.
- User-provided screenshots confirmed **real Completed and Failed Shell cards**
  side by side. Show details returned to the existing host, which displayed the
  matching persisted task ID. Replay produced a distinct task.
- `FindAll()` initially returned null before any demo tasks were created.
  Subsequent enumeration found created tasks. The adapter retains explicit null
  diagnostics because the documented return contract does not establish that
  null means empty.
- `.\Demo.ps1 -Test` passed **150 assertions**, including UTF-8/BEL/ST split points,
  malformed/unknown/oversized/truncated sequences, strict progress parsing,
  ambiguity/idempotence, all six real subprocess scenarios, cancellation, and
  stream-consumer failure propagation/cleanup. Rich-activity tests cover publication
  and history opt-out, pre-command ordering, repeated/blank titles, bounded immutable
  history, replay/reset initialization, failure/unknown/cancel semantics, and real
  CLI activity + 65% snapshots with ordered completed activities.

The original simple Completed/Failed cards above were visually observed. The new
rich title/step content and opt-in control have been built and tested at the
core/process level, but **have not been visually verified in WinUI or the Shell**.

**Not visually verified:** indeterminate/warning/unknown/crash scenarios in the
host, Cancel/close-time process cleanup, Reset view, Clear demo tasks,
HiddenByUser behavior, cold-start deep linking, restart/reboot recovery, unsupported
Windows behavior, alternate DPI/themes/accessibility modes, ARM64, and Release.
Core subprocess tests cover those protocol outcomes and cancellation, but are not
a substitute for UI or Shell verification. No displayed tasks were removed during
verification. UI automation was paused at the user's request.

The rich-activity build was registered/launched through `.\Demo.ps1` on explicit
request; the process was responsive and deployed Host/Core/Shell binaries matched
the build. No scenarios or Shell-card interactions were automated as part of that
launch. Launch verification does not verify the new rich presentation.

## First-party references

- [AppTaskInfo: support rollout, packaging, persistence, provider manifest](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskinfo?view=winrt-28000)
- [AppTaskContent factories](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskcontent?view=winrt-28000)
- [AppTaskState meanings](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskstate?view=winrt-28000)
- [Windows Terminal progress sequences](https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences)
- [Windows Terminal shell integration](https://learn.microsoft.com/en-us/windows/terminal/tutorials/shell-integration)

These APIs are experimental and gradually rolled out beginning May 2026.
Expected compiler warning **CS8305** is intentionally visible; SDK/runtime
behavior and Shell presentation can change.
