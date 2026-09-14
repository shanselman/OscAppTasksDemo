# Standalone host technical reference

Start with the [README's three-piece walkthrough](../README.md). This file
preserves implementation limits and verification details for the WinUI host;
the [native broker has its own lifecycle/cleanup policy](https://github.com/shanselman/terminal/blob/demo/osc-app-tasks/doc/shell-task-demo.md).

## Deployment terminology

"All-in-one" describes the experience, not a portable zero-prerequisite package.
The current Debug x64 host resolves `SelfContained=true`; its runtimeconfig lists
an included .NET 10 runtime. Windows App SDK self-contained deployment is not
enabled: the generated package manifest depends on `Microsoft.WindowsAppRuntime.2`
version 2.4.0.0 or later. The agent is published with `--self-contained false` and
its runtimeconfig references `Microsoft.NETCore.App` 10.0.0. Install the .NET 10
runtime for the agent and satisfy the package dependencies; do not assume the
host's private runtime makes the child CLI framework-independent.

## Protocol and content

The host launches the bundled agent with the explicit synthetic-shell-marker
fixture flag and captures redirected stdout. It is not a full terminal, ConPTY
implementation, or interception of another application's terminal.

OSC 0/2 are title metadata; application-title publication defaults off. OSC 133 A
is prompt, B is input, C begins the invocation, D;0 completes, D;nonzero fails,
and D without a code produces Unknown. The real CLI normally emits no OSC 133;
the fixture flag is direct-host-only.

OSC 9;4 states 0/1/2/3/4 represent clear/numeric/error-colored/indeterminate/warning
progress. Numeric states require a percentage in the standalone parser's supported
subset. Clear and 100% do not complete anything; style 2 does not establish final
failure, and style 4 does not establish Paused or NeedsAttention.

Unknown and Cancelled map to Shell Error **as this demo's explicit policy** with
an explanatory summary; they are never presented as successful or resumable.
A missing finish marker plus process exit 0 is Unknown. The `crash` fixture exits
7 without a finish marker; it is not an actual native-crash test.

Running content uses `CreateSequenceOfSteps`; terminal content uses
`CreateTextSummaryResult`. Percentages are text, not an invented AppTaskInfo
numeric property.

## Optional legacy step convention

Both title publication and title-to-step inference default off. The latter needs
the former and is unavailable to generic title-only/conversation fixtures.
Only the sequential legacy fixtures promise that a different in-command title
means the previous activity completed. The native generic path has no such rule.

Only titles after C participate; prior branding/cwd stays metadata. Blank and
consecutive repeated titles do not complete a step; returning to an older title
later is a new activity. Progress is invocation-level and preserved across title
changes, not assumed to be per-step. Explicit success completes the final activity;
failure, Unknown and cancellation keep it unfinished. Reset/replay creates a new
lifecycle with no stale history/title/progress.

## Bounds, privacy and lifecycle safety

- UTF-8/OSC decoding is incremental across arbitrary read boundaries, including
  BEL and ESC-backslash terminators. Invalid UTF-8 is replaced and reported;
  literal U+FFFD is not itself an encoding error. Malformed/oversized OSC is
  discarded until a terminator, and EOF reports truncation.
- OSC payloads are bounded at 4096 characters. The host retains at most 32768
  output characters and 120 decoded-event entries with visible trimming notices.
  Activity labels are sanitized/capped at 120 characters; immutable legacy
  history keeps eight completed activities plus an omitted count.
- UI/API updates are sampled at four per second during a run plus a final refresh.
  Intermediate snapshots can coalesce. Terminal lifecycle transitions are
  idempotent; later output cannot rewrite an accepted outcome.
- Execution uses a fixed bundled executable and `ArgumentList`, not an
  output-supplied shell command. Stderr is drained separately and not parsed as
  OSC by this host. Output never authorizes executing a URI or action.
- Shell receives fixed labels unless title publication was explicitly enabled.
  Titles may themselves contain prompts/paths/secrets; sanitization is not
  anonymization. Narrative and command lines are not independently published.
  Stdout lifecycle markers remain spoofable, not an authentication boundary.
- Cancel and normal close are wired to stop/reap the owned child. Hard termination
  of the host itself is not covered by a Job Object; host/OS crash recovery is
  outside this standalone demo.
- Shell tasks persist across sessions/reboots. Startup reports old tasks without
  pretending they are still live, auto-resuming, or auto-deleting them. Replay
  creates a new task; Reset view leaves persisted cards alone.
- `FindAll`/`HiddenByUser` are checked before updating an existing task. Hidden or
  removed tasks are not recreated that run. Explicit cleanup uses current-app
  enumeration plus the demo group title and safe route, never other providers.
- Activation accepts only `osctasksdemo://task/<GUID>` without query, fragment,
  credentials or port. It activates/inspects the demo, never executes output.
- Missing APIs or false support produce a prominent unsupported/local-preview
  banner. API errors show operation, exception, HRESULT and message, and stop
  publishing until an explicit new run.
- Unexpected null `FindAll` is diagnosed, not silently treated as verified empty.
  Startup may independently try Create on a run. Null enumeration during updates
  stops them because hidden state cannot be checked; clear/activation report it.
  Null content/Create results are failures, never fake-card success.

## Verification record

On 2026-09-14, Windows 26H2 build 26340.9233, Debug x64, WinApp 0.6.0:
package identity, provider/protocol manifest and bundled agent were verified.
IsSupported returned true and genuine Create/Update/readback succeeded.
FindAll initially returned null before tasks existed and subsequently returned
created tasks; diagnostics deliberately retain that distinction.

Screenshots confirmed original simple Completed and Failed Shell cards and
Show details returning to the existing demo. Later direct WinUI inspection of
legacy opt-in success showed Running tests at 65%, Inspecting project and Editing
files completed once each, and final explicit D;0 with all three completed.
The rich Shell flyout layout was not observed by those API-panel checks.

The current assertion runner passes 216 checks, including generic no-progress
sessions and presenter validation; packaged builds and non-UI presenter execution
passed. The app has been launched responsively, but launch alone does not verify
the new privacy control/generic UI.

Not visually verified here: current generic controls/cards, opt-in failure,
indeterminate/warning/unknown/crash UI, cancellation/close cleanup, Reset/Clear,
hidden-task behavior, cold activation, restart/reboot recovery, unsupported Windows,
alternate themes/DPI/accessibility, ARM64 and Release/trimming.
Do not infer these from a passing core test. No displayed cards were deleted
during this session's verification.
