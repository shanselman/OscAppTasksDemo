# Presenting generic application-title task cards

The generic model is **opt-in display metadata, not agent-brand detection**.
Application OSC 0/2 titles can supply a label; no spinner parsing, prompt parsing,
brand registry, or title-to-step/per-turn heuristic is used by the native generic
path. The optional sequential-step convention in the standalone host is a
different, explicitly labeled legacy fixture, disabled for the generic scenarios.

## One invocation from this repository

In your current terminal:

```powershell
.\Show-Demo.ps1 -Build
```

Default mode is `Terminal`, default scenario is `conversation`, default stage pace
is 1600 ms. `-Build` invokes the existing `Demo.ps1 -AgentOnly` entrypoint to publish
`artifacts\agent\OscTasks.Agent.exe`; it does not build/register/start WinUI or a
native Terminal. The agent is framework-dependent and requires .NET 10.
Subsequent invocations can omit `-Build` to use the existing published binary.
Rebuild explicitly after updating source; the script does not detect stale binaries.

In an ordinary terminal you will see output/title changes, not automatic Shell
cards. The Shell experience requires the separate custom Terminal build, its
registered provider, an integrated shell, and the native feature opt-in.
The OSC repository does not install these prerequisites.

### Native Terminal handoff

The coordinated native fork/branch supplies this separate launcher, run from its
own checkout after following that branch's build/package prerequisites:

```powershell
.\samples\ShellTaskDemo\Start-Demo.ps1 -OscDemoPath <path-to-this-checkout> -MirrorTitles
```

It opens an isolated integrated shell exposing `$DemoAgent`. In that shell:

```powershell
& $DemoAgent title-only --delay-ms 2000
& $DemoAgent conversation --delay-ms 2000
& $DemoAgent success --delay-ms 2000
& $DemoAgent failure --delay-ms 2000
```

The native launcher also supports an explicit `-AgentPath <published-exe>` and
`-CheckOnly` preflight. It uses process-local `WT_ENABLE_SHELL_TASKS=1`; the separate
`WT_SHELL_TASK_TITLES=1` opt-in corresponds to `-MirrorTitles`. Without title opt-in,
labels stay fixed. Native launching/building and package registration belong to
that repository, not `Show-Demo.ps1`; this document specifies the coordinated
interface rather than bundling or downloading the native implementation.
The native peer published it on `shanselman/terminal` branch `demo/osc-app-tasks`,
commit `055a2ce8de96987c27dc604f86893a16a4e15c33`:
[native walkthrough](https://github.com/shanselman/terminal/blob/055a2ce8de96987c27dc604f86893a16a4e15c33/doc/shell-task-demo.md)
and [launcher source](https://github.com/shanselman/terminal/blob/055a2ce8de96987c27dc604f86893a16a4e15c33/samples/ShellTaskDemo/Start-Demo.ps1).

No fixture runs automatically inside the launched shell. Its genuine foreground
command lifecycle owns C/D. Do **not** add `--synthetic-shell-markers` to these
commands. The OSC script neither modifies profiles nor injects shell commands.

### Standalone host

```powershell
.\Show-Demo.ps1 -Mode Standalone -Build
```

This builds and launches the existing packaged WinUI host through `winapp run`.
Without `-Build`, it uses an existing host build. Select the scenario in the UI;
`-Scenario`/`-DelayMs` apply only to Terminal mode. The host's title-publication
checkbox is **off by default**, independently of the legacy step-history checkbox.
For title-only/conversation, step history cannot be enabled.

The direct-process WinUI host requests synthetic markers explicitly because it
contains no shell. That fixture flag is not part of the normal coding-agent pattern.

## Bounded walkthrough

| Scenario | Emitted metadata | Point to explain |
|---|---|---|
| `title-only` | Changing generic OSC 2 titles; no OSC 9;4 | An open session with a title is **Session active**, not evidence that it is working. |
| `conversation` | Titles and an answer, then a bounded open interval; no OSC 9;4 | The answer is not process exit or an agent-turn event. The card stays **Session active** until the foreground process actually ends. |
| `success` | Titles; 10/45/65/100% progress; clear; later exit 0 | 100% and clear do not complete the task. Clear returns to **Session active**. |
| `failure` | Titles; progress; error-colored progress; exit 1 | Styling alone is not terminal failure; the real exit/lifecycle outcome supplies it. |

The title-only and conversation fixtures emit Sample session, Sample response
(twice), and Sample session available. Repeats are metadata, not another step.
Narrative is never parsed into card structure. Neither fixture emits progress or
per-turn markers; normal CLI mode emits no OSC 133 at all.

`--delay-ms N` accepts integer 0..10000; `--fast` remains available but cannot be
combined with it. Conversation's post-answer interval is at least two seconds at
nonzero normal pace (4.8 seconds at default pace), at most 30 seconds. Explicit
`--fast` or `--delay-ms 0` removes that visible wait for tests. All scenarios exit
without user input; none creates an indefinite unattended wait.

The legacy indeterminate/warning/unknown/crash scenarios remain available.
`unknown` means a missing outcome only in synthetic-fixture mode; in normal mode
its process exits 0 and the real shell can report success.

## Semantics and privacy

- **Session active** means the observed foreground process/session remains open.
  It says nothing about thinking, waiting for a user, or completion of an answer.
  The standalone lifecycle begins at C; it does not infer a start from a title.
- **Working** is qualified by application OSC 9;4 progress, not by title text,
  animation, CPU use, silence, brand, or narrative. Clear removes that progress
  indication without ending the foreground process.
- OSC 133 describes the foreground command/process, not turns within a long-lived
  interactive agent. Missing lifecycle must remain unknown; OSC 133 has no nested
  task/producer IDs and cannot authenticate a misbehaving child's finish marker.
- Error/warning styling does not imply terminal Error or NeedsAttention. An
  application title saying "done" or "waiting" has no semantic authority.
- Accept titles only through a separate opt-in. They may contain private prompts
  or paths. Removing control characters and bounding length is **not anonymization**.
  Shell cards persist; publish only when that exposure is acceptable.
- The native contract accepts application titles during C..D, without prefilling
  from a prior shell prompt. The standalone bounds activity labels at 120 characters;
  ordinary stdout/stderr stay local. Title text never selects a task or activation
  route, supplies a command, or authorizes an action.
- Hidden, removed, stale and persisted cards still require the provider's existing
  lifecycle/cleanup rules. A title change must not recreate dismissed cards or make
  an old route valid. Neither presenter script deletes cards.

## Real-agent evidence: narrow observations, not a support registry

Parent-session calibrated ConPTY captures on **2026-09-14**, approximately
15:18-15:19 Pacific, observed these interactive modes at 140x40 for about 55 seconds
each, using a short synthetic arithmetic question:

| Agent/version observed | Mode caveat | Metadata observed in this sample |
|---|---|---|
| Claude Code **2.1.271**, from final startup banner | Safe mode, tools disabled; an earlier version check was 2.1.220 before native auto-update. | OSC 0 application-title changes; no agent OSC 9;4 or OSC 133. |
| GitHub Copilot CLI **1.0.84-6**, from version output | Interactive prompt with no-custom-instructions, disabled built-in MCPs, empty available-tools, no-auto-update; some existing customization still loaded, so not configuration-free. | OSC 0 application-title changes; no agent OSC 9;4 or OSC 133. |
| Codex CLI **0.154.0** | Interactive, read-only sandbox, on-request approval, existing user configuration, launched through its command wrapper. Wrapper startup title excluded. | OSC 0 application-title changes; no agent OSC 9;4 or OSC 133. |

Each answered the simple question. These are not observations of long-running
work, approvals, or every CLI mode/version; they do not establish universal
absence/presence of a protocol. Some titles included animated decorations or
synthetic prompt text, which is exactly why the bridge must not interpret spinner
glyphs or assume titles are privacy-safe.

Before the final captures, a calibration fixture demonstrated that title OSC 0,
OSC 9;4 indeterminate/numeric/clear, and OSC 133 C/D all survived the actual
**ConPTY backend 0**. Initial **legacy WinPTY backend 1** captures dropped
progress/lifecycle; those negative observations are invalid and excluded.
No shell-integration script wrapped the final agent captures. Raw captures,
transcripts, paths and session identifiers are intentionally not published.

Users' real agent CLIs need no bridge-specific code changes to contribute whatever
application titles they already emit. Launch them normally in the configured
integrated shell, with title publication explicitly enabled if acceptable. Do not
promise numeric progress when the app does not emit it, or infer per-turn completion
from title changes. The native generic bridge is not a brand compatibility list.

## Checks and remaining verification

`.\Demo.ps1 -Test` covers default/private title behavior, progress-qualified
wording, clear/100/style ambiguity, no step inference, both marker modes,
bounded answer/waiting fixtures, pacing validation and presenter argument plans.
Use `.\Show-Demo.ps1 -ValidateOnly` to inspect a JSON invocation plan without
building, launching, changing settings, or emitting a fixture.

Actual presenter CLI execution was checked for marker-free/no-progress title-only
output, failure exit propagation, and restoration of the caller's directory.
The packaged host builds; the new publication checkbox/generic fixtures have not
been visually verified here. Earlier opt-in legacy-step API readbacks do not
verify these changes. A native Shell success screenshot was reported separately;
title-aware native card rendering is still awaiting the parent integration review.

See [protocol/design details](rich-osc-task-cards.md) and
[pinned upstream reconnaissance](intelligent-terminal-integration.md).
