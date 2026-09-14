# Rich task cards from terminal metadata

**Compatible activity/step tier implemented; structured extension still proposed.**
Preserve existing OSC compatibility first; richer interpretations require explicit
producer/host agreement. No new OSC protocol is implemented.

## What exists today

The demo already separates the byte-stream parser, one-invocation lifecycle,
`AppTaskInfo` adapter, and WinUI presentation. It captures a fixed child process's
stdout, not a full terminal or another terminal application's output.

It decodes OSC 0/2 titles, OSC 9;4 progress, and OSC 133 lifecycle markers. The fixed
fake agent emits Inspecting project, Editing files, and Running tests as activity
titles. The host publishes these sanitized labels only after a separate,
default-off privacy opt-in, keeping its fixed group title and safe task route.
Generic title-only/conversation fixtures emit no progress and never infer steps.

A separate, default-off legacy fixture checkbox enables a title-to-step convention
only after publication opt-in, and not for the generic fixtures.
Running cards use genuine `CreateSequenceOfSteps` content; final summaries preserve
completed history and label interrupted activities unfinished. Successful lifecycle
completion can finish the final activity; 100%, clear, and narrative cannot.
History retains eight completed activities with an omitted count and resets per run.
The reusable lifecycle defaults title publication and history off for arbitrary
sources. Structured task events and concurrent tasks are **not** implemented.
The native generic path treats titles purely as display metadata, with no
title-step/brand/spinner/per-turn heuristic. Open sessions with no application
progress say Session active; Working is qualified by OSC 9;4. See the
[generic presenter and evidence note](generic-agent-demo.md).

## Compatible tier: title + progress + lifecycle

**OSC 9;4 carries numeric state/percentage, not text.** Combine separate signals:

| Signal | Existing meaning | Possible bridge use |
|---|---|---|
| OSC 0/2 | Window/icon title metadata | Current activity label, only when explicitly agreed |
| OSC 9;4 | Numeric or indeterminate progress and styling | Local progress plus textual percentage in Shell content |
| OSC 133;C | Command execution/output begins | Start one invocation's task |
| OSC 133;D;\<exitcode\> | Command finishes with an outcome | Complete or fail the invocation |

An illustrative stream follows. `ESC`, `BEL`, and `ST` below denote actual control
characters, not literal words: ESC is U+001B, BEL is U+0007, and ST is ESC followed
by backslash. Narrative lines are ordinary stdout.

```text
ESC ] 133;C BEL
ESC ] 2;Inspecting project ST
Reading project files...
ESC ] 9;4;1;10 BEL
ESC ] 2;Generating changes ST
Preparing a candidate change...
ESC ] 9;4;1;45 BEL
ESC ] 2;Running tests ST
Executing the checks...
ESC ] 9;4;1;65 BEL
```

The **implemented, opt-in interpretation** is intended to produce content like this
at that point (exact Shell rendering of the new rich content remains unverified):

```text
Demo agent
Running tests -- 65%
Completed steps: Inspecting project; Generating changes
Current step: Running tests
[Show details]
```

Here `Demo agent` is a host-controlled task/group label, not an inferred title.
The producer must explicitly opt into a convention that activity-title changes
represent sequential steps and that a new activity means the previous step
completed. **That convention is not standardized OSC semantics.** A normal title
could be a working directory, shell name, command, or arbitrary text. Without
agreement, keep it as title metadata and do not invent step history. Even with
agreement, a step transition is not completion of the whole task.

In this demo, only titles after `OSC 133;C` count as activities. Empty and consecutive
repeated titles are ignored as transitions. Progress stays invocation-level across
activity changes; no per-step percentage is inferred. The concrete CLI says
**Editing files** rather than **Generating changes**, but the convention is the same.

Later, explicit success might be:

```text
ESC ] 9;4;1;100 BEL
Final checks still running...
ESC ] 9;4;0;0 BEL
ESC ] 133;D;0 ST
```

100% and clear do not authorize success. Warning-colored progress is not
necessarily `NeedsAttention`, and error-colored progress is not terminal failure.
A missing exit code leaves the outcome unknown. OSC 133;B denotes command input,
not execution. Real shells normally supply lifecycle markers; this demo CLI
leaves them to the shell **by default**. The standalone WinUI process-output host
explicitly requests `--synthetic-shell-markers` as a test fixture because it runs
no shell. The wire examples above illustrate the combined stream, not a contract
for coding agents to emit shell-owned OSC 133 themselves.

For custom-terminal tests, run the CLI normally under an integrated shell. Mixing
synthetic markers with that shell's own markers creates ambiguous nesting: OSC 133
has no producer/task ID. Duplicate-transition guards are useful against repeated
or misbehaving output, but cannot authenticate a finish event or resolve ownership.
Meaningful activity titles, progress and a real exit code are the agent pattern;
shell lifecycle comes from the shell. See [CLI modes](../README.md#normal-coding-agent-mode-vs-direct-host-fixture).

Ordinary output remains narrative. Do not automatically convert log lines into
steps, questions, buttons, or results.

## What the Shell can represent

Documented `AppTaskContent` supports sequences of completed/executing steps,
text summaries, preview thumbnails, generated assets, questions, buttons, and
text input. These richer content forms are not all exercised by this demo.
There is **no documented numeric percentage property**: percentage can be text
in a subtitle or executing-step label, while a local preview renders a progress bar.

Content capability is not proof that every Windows Shell version will display
every form. Keep a readable text fallback, accessible labels, and non-color-only
state indicators. Check runtime API presence and `IsSupported`; show exact
unsupported/failure diagnostics rather than simulating a successful Shell card.
Actual API readback and actual Shell visibility remain separate evidence.

## Optional structured extension -- PROPOSED

Richer scenarios need explicit identities and events rather than title heuristics.
The following is a **PROPOSED payload illustration**, not a standard, allocated
OSC sequence, supported schema, or promise of implementation:

```json
{
  "schema": "PROPOSED.task-metadata",
  "version": 1,
  "taskId": "task-7",
  "sequence": 4,
  "event": "progress",
  "data": {
    "activity": "Running tests",
    "percent": 65,
    "completedSteps": ["Inspecting project", "Generating changes"],
    "currentStep": "Running tests"
  }
}
```

Possible **PROPOSED** event vocabulary:

| Event | Intended information |
|---|---|
| `begin` | Task identity and display intent within an authorized producer session |
| `step` | Explicit step identity and transition; no inference from ordinary titles |
| `progress` | Numeric/indeterminate progress and optional bounded display snapshot |
| `needs-attention` | Explicit question or input request, with host-approved action identifiers |
| `result` | Summary and approved thumbnail/generated-asset references |
| `end` | Explicit final outcome, correlated to this task |

This could support concurrent tasks, explicit step completion, questions, approved
actions, and result assets. However, transport framing, namespace/OSC allocation,
capability negotiation, reply/input transport, versioning, and conflict resolution
are **unresolved**. Do not hijack an existing OSC number or stuff JSON into OSC 9;4.
No numeric code is proposed here.

Existing OSC remains the baseline. A producer must learn that a host supports the
extension before emitting it; legacy hosts should still get narrative and normal
title/progress/lifecycle behavior. A negotiated host must deduplicate baseline and
structured signals, not create two tasks or apply two terminal transitions.
Per-command OSC 133 cannot by itself identify which concurrent logical task ended.

## Trust, boundaries, and persistence

- Scope producer task IDs to a host-generated session/stream identity; they must
  not select another producer's or provider's task. Bind multiplexed events to
  authorized channels, enforce sequence/order rules, and reject stale/replayed
  events. Metadata on stdout is spoofable: capability negotiation alone is not
  authentication. Decide which child processes may publish task metadata.
- Validate schema, event/state transitions, IDs, strings, numeric ranges, URIs,
  and assets. Bound payload size, JSON depth, step/task counts, retained history,
  and update rates. Preserve incremental parsing and bounded recovery; do not
  remove the current parser/UI limits to accommodate a proposal.
- Define UTF-8 and framing/escaping precisely. Embedded ESC, BEL, ST, newlines,
  or delimiters must not terminate/inject another message. JSON escaping alone
  does not decide OSC framing. Reject invalid encoding and sanitize display
  controls/bidirectional formatting without interpreting text as markup.
- Keep activation/action routes host-owned. Untrusted metadata may request a
  logical action; the host maps an allowlisted identifier to a safe route after
  validation and any required user approval. Never execute arbitrary commands,
  shell fragments, or output-supplied URIs. A question is not permission to act.
- Result assets require scoped, validated references and lifetime rules; no
  automatic fetch of arbitrary URLs or unrestricted local-file access. Define
  how input replies return to the correct still-live producer.
- Moving activity titles into Shell is a privacy change from the original demo.
  Titles/logs can contain secrets, paths, or personal data. Require explicit opt-in,
  safe labels/redaction, and a clear publication policy. Persisted cards outlive
  the process; do not publish raw narrative by default.
- Respect `HiddenByUser` and external removal; do not resurrect dismissed cards.
  Keep host task mappings separate from producer IDs, handle stale persisted
  tasks after restart honestly, and offer cleanup scoped to demo/provider-owned
  tasks. Replay, reconnect, and restart are not automatic authority to resume work.

## Roadmap and open decisions

1. Title + progress + lifecycle composition is implemented with a separate
   default-off publication opt-in. Visually validate its rich
   Shell rendering before making new presentation claims.
2. Default-off title-to-step history is implemented with documented producer
   guarantees, bounded history, and tests for pre-command/repeated titles and
   failure handling. It remains a legacy standalone fixture option, unavailable
   to generic title-only/conversation scenarios; do not generalize it to arbitrary shell titles.
3. Design negotiated structured metadata later, with explicit task identity,
   action/input policy, fallback behavior, and conformance tests.
4. Consider a `microsoft/intelligent-terminal` fork/integration only after examining
   its architecture and stream handling. [Pinned read-only findings](intelligent-terminal-integration.md)
   now identify parser, pane-event, packaging, and activation seams. No fork or
   integration is being made now.

Key tradeoffs: title conventions are simple but ambiguous; structured metadata is
precise but requires adoption and trust policy. Choose snapshot versus delta
events, baseline-versus-extension outcome authority, and behavior for disconnects,
out-of-order events, cancellation, and task-ID reuse before implementation.
Transport/namespace ownership and capability discovery need agreement, not an
invented code in a sample.

Keep parser, task lifecycle, Shell adapter, and demo UI separate throughout. A
future terminal transport should replace the input source, not entangle Shell
APIs with the CLI or parser.

## First-party references

- [Windows Terminal progress sequences](https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences)
- [Windows Terminal shell integration](https://learn.microsoft.com/en-us/windows/terminal/tutorials/shell-integration)
- [AppTaskInfo: support, packaging, persistence, and activation](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskinfo?view=winrt-28000)
- [AppTaskContent: supported content factories and interactions](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskcontent?view=winrt-28000)
- [AppTaskState meanings](https://learn.microsoft.com/en-us/uwp/api/windows.ui.shell.tasks.apptaskstate?view=winrt-28000)
