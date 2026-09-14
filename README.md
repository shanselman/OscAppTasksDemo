# OSC App Tasks Demo

Turn ordinary command-line activity into a **real Windows Shell task card**, using
existing terminal escape sequences rather than an agent-specific integration.

## Three pieces, two ways to try it

| Piece | What it does | Where to start |
|---|---|---|
| **1. All-in-one Windows demo app** | A packaged WinUI window bundles the fake CLI and shows process output, decoded OSC, local state, and real `AppTaskInfo` results side by side. **No custom Terminal needed.** | [Route A](#route-a-all-in-one-windows-demo) |
| **2. Independent CLI demo app** | `OscTasks.Agent` emits safe sample titles, progress, narrative, and a process exit code. No WinRT, package identity, agent login, network, or model service. | [CLI/presenter commands](#the-cli-and-presenter-script) |
| **3. Custom Windows Terminal Dev** | The native Terminal owns the packaged broker and publishes cards for ordinary foreground CLI programs. The Shell draws a separate task surface; Terminal does not paint an imitation. | [Our fork branch][native-branch], [native guide][native-guide], [launcher][native-launcher], [Route B](#route-b-cli-inside-the-custom-terminal) |

The Windows app is a **self-contained demonstration experience**, not a promise
of zero runtime/package prerequisites. The current Debug x64 WinUI host includes
its .NET runtime, but depends on the Windows App SDK framework package; its bundled
CLI is explicitly **framework-dependent** and requires installed .NET 10.
The native Terminal is C++/WinRT; .NET 10 is needed for this sample CLI, not for
Terminal itself.

This is a fork demonstration, **not stock Windows Terminal support, an upstream
PR, or an upstream-approved design**.

## Prerequisites

| For | Requirements |
|---|---|
| Real Shell cards | Windows 11 with the experimental App Tasks rollout available. APIs began gradual rollout in May 2026; build number alone is not proof. The provider checks API presence/support and reports failures. |
| Building/running this CLI | .NET **10 SDK** to build; .NET **10 runtime** to run the published framework-dependent executable. PowerShell 7 for presenter scripts. |
| Route A: WinUI host | Developer Mode, current WinApp CLI (`winapp` >= 0.3; 0.6.0 used here), package identity and Windows App SDK runtime dependencies. The build restores Windows App SDK **2.4.0**, SDK BuildTools **10.0.28000.2705**, BuildTools.WinApp **0.6.1**, and Windows SDK projection **10.0.26100.87**. Older projections may lack AppTaskInfo. |
| Route B: native Terminal | Follow the fork's [native build workflow][native-building] and [demo build/registration instructions][native-guide]. Native C++ **UWP/XAML** tooling is required, not just desktop C++ Build Tools. The verified build used VS2026 with UWP C++ v145 and a serviced Windows SDK 10.0.26100.0 containing the AppTask contract/projection. Submodules and repository NuGet configuration matter. |

Debug x64 is the validated demo build configuration. The OSC script accepts
ARM64, but ARM64, Release/trimming, and unsupported Windows behavior have not been
verified. Scripts do not install prerequisites, trust certificates, or change
profiles/system settings. In a WinUI-enabled Copilot environment, `/winui-setup`
can help establish missing Route A prerequisites.

## Get the OSC repository

**From a PowerShell 7 window**, choose a parent directory. These examples use
sibling checkouts under your home directory; change `$DemoRoot` if desired:

```powershell
$DemoRoot = Join-Path $HOME 'source\osc-shell-demo'
New-Item -ItemType Directory -Force $DemoRoot | Out-Null
Set-Location $DemoRoot
git clone https://github.com/shanselman/OscAppTasksDemo.git
Set-Location .\OscAppTasksDemo
```

All Route A commands below run **from the `OscAppTasksDemo` checkout**.

## Route A: all-in-one Windows demo

**From `OscAppTasksDemo`:**

```powershell
.\Demo.ps1
```

This publishes the agent, builds the WinUI host, registers its development
package with WinApp, and launches it with package identity. It stays attached
for debug output until the app exits. Never launch `OscTasks.Host.exe` directly,
remove its manifest, or substitute an unpackaged build.

In the window, choose **conversation** or **title-only**, then **Run / replay**.
You can see changing titles in the decoded stream while the default Shell label
stays fixed. Enable **OPT-IN: publish application titles** before another run
only if exposing those titles is acceptable. The new choices apply per run.

For a progress demonstration, choose **success**: 10%, 45%, 65%, 100%, clear, and
then the explicit process outcome. Choose **failure** to show error styling
followed later by failure. Only the actual outcome finishes the task.

The optional **legacy step fixture** checkbox interprets sequential title changes
as completed activities. It is explicitly nonstandard, requires title opt-in,
and is disabled for generic title-only/conversation fixtures. **The native generic
Terminal path never uses this title-to-step heuristic.**

The left pane is redirected stdout, **not a terminal emulator or ConPTY**.
The right pane distinguishes actual API results from a labeled local preview.
On supported Windows, inspect the separate Shell card. **Reset view** only clears
the host's view; **Clear demo tasks** explicitly removes its persisted demo cards.
Leave cards alone until you are done presenting.

**From `OscAppTasksDemo`, for alternate entrypoints:**

```powershell
.\Show-Demo.ps1 -Mode Standalone -Build  # Same packaged demo route
.\Demo.ps1 -BuildOnly                  # Build; do not register/relaunch
.\Demo.ps1 -Test                       # Automated checks, no Shell cards
```

Close the host before a normal rebuild/relaunch. A build-only operation does not
refresh an already registered layout or a running instance.

## Route B: CLI inside the custom Terminal

Use **[the `demo/osc-app-tasks` branch of our Terminal fork][native-branch]**,
not an installed stock Terminal. The native package is built/registered separately.
Detailed prerequisites, build, loose-layout registration, diagnostics and manual
checks live in the [native guide][native-guide]; the
[presenter launcher source][native-launcher] is the reproducible launch contract.

**From the same parent directory used above** (redefine `$DemoRoot` in a new shell):

```powershell
$DemoRoot = Join-Path $HOME 'source\osc-shell-demo'
Set-Location $DemoRoot
git clone --branch demo/osc-app-tasks --recurse-submodules https://github.com/shanselman/terminal.git TerminalDemo
```

**From `OscAppTasksDemo`, publish the marker-free fixture:**

```powershell
Set-Location (Join-Path $DemoRoot 'OscAppTasksDemo')
.\Demo.ps1 -AgentOnly
```

This needs no WinUI/WinApp setup. The result is
`OscAppTasksDemo\artifacts\agent\OscTasks.Agent.exe`.

**From `TerminalDemo`, follow the [native guide][native-guide] to build and
register the Debug x64 Dev package**, using its required developer toolchain.
Do not assume an unsigned MSIX can be installed by double-clicking. Use the
documented developer/loose-layout registration workflow; do not bypass certificate
or OS warnings or add unapproved certificate trust.

Then **from `TerminalDemo`, in non-elevated PowerShell 7**, close existing
**Dev** windows yourself and run:

```powershell
$DemoRoot = Join-Path $HOME 'source\osc-shell-demo'
$OscCheckout = (Resolve-Path (Join-Path $DemoRoot 'OscAppTasksDemo')).Path
Set-Location (Join-Path $DemoRoot 'TerminalDemo')
.\samples\ShellTaskDemo\Start-Demo.ps1 -OscDemoPath $OscCheckout -MirrorTitles -CheckOnly
.\samples\ShellTaskDemo\Start-Demo.ps1 -OscDemoPath $OscCheckout -MirrorTitles
```

`-MirrorTitles` is an explicit privacy choice for this synthetic demonstration.
Omit it for the fixed-label comparison. The first non-elevated Dev process owns
its feature environment until it exits: setting variables inside an existing pane
does not enable it. Close Dev and relaunch to change the opt-ins. The launcher
checks for existing Dev processes and never kills them.

It sets process-local `WT_ENABLE_SHELL_TASKS=1` and independently controls
`WT_SHELL_TASK_TITLES=1`, starts an isolated integrated shell, and exposes
`$DemoAgent`. It does not modify permanent profiles/environment or auto-run agents.
An explicit `-AgentPath <published-exe>` is also supported.

**Inside the newly opened custom Terminal demo shell**, type these normal
foreground commands; no particular working folder is required because
`$DemoAgent` is an absolute path:

```powershell
& $DemoAgent title-only --delay-ms 2000
& $DemoAgent conversation --delay-ms 2000
& $DemoAgent success --delay-ms 2000
& $DemoAgent failure --delay-ms 2000
```

The native demo skips commands shorter than two seconds and keeps at most one
card per connection; a new command can replace the preceding card. In
**conversation**, the simulated answer is followed by a bounded open interval.
The card remains **Session active**: an answer is not process exit or an agent turn
completion signal. Clear/100% never supply completion.

## The CLI and presenter script

**From `OscAppTasksDemo`, in your current terminal:**

```powershell
.\Show-Demo.ps1 -Build
.\Show-Demo.ps1 -Scenario title-only
.\Show-Demo.ps1 -Scenario success -DelayMs 2000
.\Show-Demo.ps1 -Scenario failure -DelayMs 2000
.\Show-Demo.ps1 -ValidateOnly
dotnet run --project .\OscTasks.Agent -- --help
```

The presenter defaults to marker-free **conversation**, publishes through
`Demo.ps1 -AgentOnly` only when `-Build` is requested, and otherwise uses the
existing agent executable. Rebuild explicitly after source updates.
It does **not** launch/install native Terminal. In a stock terminal you get output
and whatever OSC features it supports, not AppTask cards.

Scenarios: `title-only`, `conversation`, `success`, `failure`, `indeterminate`,
`warning`, `unknown`, and `crash`. Pace is bounded by `--delay-ms 0..10000` or
mutually exclusive `--fast`. Conversation waits at least two seconds after its
answer at normal nonzero pace (default 4.8 seconds), then exits. No indefinite
unattended input is required. `unknown` omits its outcome only in explicit fixture
mode; the real CLI process still exits 0. `crash` is a simulated exit 7, not a
native crash-dump test.

See the [generic presenter guide](docs/generic-agent-demo.md) for scenario details.

## What a CLI author needs to do

There are **three separate responsibilities**, not a CLI self-registration API:

| Owner | Responsibility |
|---|---|
| **CLI** | Optionally emit supported, safe existing OSC metadata and return a meaningful process exit code. No WinRT/package dependency, brand registration or new protocol. A title alone is useful; percentages are optional. |
| **Shell** | Its shell integration supplies OSC 133 C/D for foreground command lifetime. The CLI must not emit these shell-owned markers under a real integrated shell. |
| **User / host** | Enable task publication and, separately, consent to application-title mirroring. In this Dev fork those are the launcher/feature options above, not something a CLI may silently authorize. |

**Illustrative C# CLI source**, not a command to run in either checkout:

```csharp
bool emitOsc = !Console.IsOutputRedirected &&
               Array.IndexOf(args, "--no-osc") < 0;
void Osc(string payload)
{
    if (!emitOsc) return;
    Console.Write($"\x1b]{payload}\x07"); // ESC ] payload BEL
    Console.Out.Flush();
}

Osc("2;Preparing sample report"); // Safe display metadata, not task identity
Osc("9;4;3;0");                 // Report indeterminate progress, if appropriate
Console.WriteLine("Preparing the sample report...");
// Perform actual work; report its real progress rather than inventing a percentage.
Osc("9;4;1;65");
Osc("9;4;0;0");                 // Hide progress, NOT a completion signal
return 0;                       // Use a nonzero exit code if the work failed
```

The exact title sequence is `\x1b]2;Preparing sample report\x07`. OSC 0 can also
set a title; `\x1b\\` (ESC followed by backslash) can terminate OSC instead of BEL.
This example intentionally emits **no OSC 133**.
It illustrates encoding, not actual report work; the native demo skips commands
that finish within two seconds. Use the paced fixtures for a visible presentation.

Prefer a terminal-attached stream, normally stdout, with an opt-out/no-escape
mode when redirected. If choosing stderr, check whether it is terminal-attached
and whether the intended host consumes it; the standalone demo parses stdout only.
Our test CLI deliberately retains sequences in redirected stdout so the WinUI
fixture can capture them. That is a harness choice, not a universal author default.
Support for OSC 9;4 and passthrough through tmux/SSH/other hosts must be tested,
not assumed. Never put secrets or private prompt/path text in a demo title.

The WinUI host explicitly passes `--synthetic-shell-markers` because it runs
the CLI directly without a shell. **Do not pass that flag in Route B.**
OSC 133 has no nesting/producer IDs; a misbehaving child can emit ambiguous
duplicate starts/finishes. Idempotence helps with duplicates but does not authenticate
an outcome. One agreed lifecycle producer is required.

## How the light-up works

```text
CLI: optional OSC 0/2 title + optional OSC 9;4 progress + narrative + exit
Shell: OSC 133 C/D around foreground execution
    -> host's existing parser and lifecycle
    -> packaged owner/broker calls AppTaskInfo
    -> Windows Shell renders its separate task surface

Show details -> opaque host-owned route -> matching live tab/pane or demo window
```

Titles are sanitized/bounded **display metadata**, not authenticated application
identity, commands, step completions or agent turns. Routes are independent of
titles. Native click-back is implemented: a live route selects its tab/pane/window;
invalid or stale routes show an explanation rather than rerunning a saved command.
**Live native click-back verification is still unconfirmed.**

| Input | What may be claimed |
|---|---|
| Open foreground session, no progress | **Session active**, not necessarily working or waiting for required input |
| OSC 9;4 numeric/indeterminate | **Reported application progress**, not inferred work |
| Clear or 100% | Still open until an authoritative outcome |
| Error/warning progress styling | Not terminal failure, Paused, or NeedsAttention by itself |
| Shell C/D | Foreground command lifetime, **not turns inside an interactive agent** |

The sample native PowerShell integration maps its overall command/pipeline
success to D;0 or D;1, not an exact arbitrary native exit code. Other shell
integrations, nested shells and concurrent background jobs require validation.
`AppTaskContent` has no documented numeric-percent property: percentage is text
in the card; optional richer factories are described in the design note.

## What you get without changing an agent

Three real interactive agent CLIs—Claude Code, GitHub Copilot CLI and Codex—emitted
OSC 0 titles in calibrated, short arithmetic probes. Those already available
titles can provide opt-in task-card metadata without agent-specific code or a
brand registry. Shell-owned lifetime provides Session active and eventual process
completion; the host provides a route back to the session.

No agent OSC 9;4 or OSC 133 appeared in those short samples. That is **not universal
absence** across versions, modes, long jobs or approval states. You get progress
only if the program reports it; an interactive process does not complete merely
because it printed an answer. We do not parse spinners or infer per-turn
work/approval/completion. See [versions, modes and calibrated evidence](docs/generic-agent-demo.md#real-agent-evidence-narrow-observations-not-a-support-registry).

**Privacy:** titles can contain complete prompts, private paths or secrets.
Sanitization is not redaction. Mirroring publishes to a Shell-owned persistent
surface outside Terminal, so consent is separate from enabling task cards.

## Verification, limits and what comes next

- **OSC repo:** 216 assertions pass, including byte-split parsing, normal/fixture
  modes, lifecycle ambiguity, bounded waiting/history, privacy defaults and
  presenter validation. Packaged Debug x64 builds; presenter execution was checked
  for marker-free titles, failure exit propagation and caller-directory restoration.
- **Observed standalone:** real simple Completed/Failed Shell cards and return to
  the demo window; legacy opt-in rich API readback at 65% and final all-three
  completed activities. API readback is not proof of rich Shell flyout layout.
- **Observed native:** a screenshot of an ordinary CLI success producing a genuine
  Shell Completed card. Latest title-aware rendering, failure and Show details
  click-back remain unconfirmed. Native focused tests/build and detailed gaps are
  tracked in the [native guide][native-guide].
- **Still unverified here:** new generic host privacy controls/rendering, pane
  moves/stale routes/dismissal/restart behavior, unsupported systems, alternate
  accessibility/themes/DPI, ARM64 and Release. No manual checklist is represented
  as passed solely because code builds.

Next: verify live title-aware cards, success/failure and click-back across pane
moves/stale routes; harden production settings, localization and persistence;
discuss generic standards or negotiated explicit richer metadata rather than
per-agent heuristics. The richer JSON extension is **proposed, not implemented**.
Any upstream discussion/contribution must respect the upstream project's design
and AI-contribution policies. Publishing this fork demo is not upstream approval.

## Code and deeper documentation

| Location | Responsibility |
|---|---|
| `OscTasks.Agent` | Independent deterministic CLI |
| `OscTasks.Core` | Platform-neutral incremental parser, lifecycle and process transport |
| `OscTasks.Shell` | Real packaged AppTaskInfo adapter; no WinUI dependency |
| `OscTasks.Host` | Packaged WinUI presentation and orchestration |
| `OscTasks.Tests` | Dependency-free assertion runner and actual subprocess tests |

- [Generic presenter, empirical evidence and native handoff](docs/generic-agent-demo.md)
- [Standalone technical reference and known gaps](docs/standalone-reference.md)
- [Rich content / optional metadata design](docs/rich-osc-task-cards.md)
- [Pinned upstream reconnaissance and later fork status](docs/intelligent-terminal-integration.md)
- [Custom Terminal fork branch][native-branch] · [Native guide][native-guide] · [Native launcher][native-launcher]
- First-party references: [AppTaskInfo](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskinfo?view=winrt-28000),
  [AppTaskContent](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskcontent?view=winrt-28000),
  [AppTaskState](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskstate?view=winrt-28000),
  [Terminal progress OSC](https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences),
  [shell integration](https://learn.microsoft.com/en-us/windows/terminal/tutorials/shell-integration).

[native-branch]: https://github.com/shanselman/terminal/tree/demo/osc-app-tasks
[native-guide]: https://github.com/shanselman/terminal/blob/demo/osc-app-tasks/doc/shell-task-demo.md
[native-launcher]: https://github.com/shanselman/terminal/blob/demo/osc-app-tasks/samples/ShellTaskDemo/Start-Demo.ps1
[native-building]: https://github.com/shanselman/terminal/blob/demo/osc-app-tasks/doc/building.md
