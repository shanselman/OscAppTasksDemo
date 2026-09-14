# intelligent-terminal integration reconnaissance

Read-only source inspection on 2026-09-14, pinned to
[`microsoft/intelligent-terminal@8ba6fdef80b80acabb4ff269b1c0aab9e9970c55`](https://github.com/microsoft/intelligent-terminal/tree/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55).
No fork, clone, upstream modification, or integration was performed. These are
source findings and a proposed integration path, not runtime verification.

**Conclusion:** existing OSC parsing and pane routing provide useful attachment
points. Start with a pane-scoped bridge in TerminalApp, a separate task-lifecycle
registry, and a packaged Shell publisher. Do not build it on the aggregated
taskbar progress indicator or assume the agent IPC stream already carries every
required signal.

## Verified source paths

| Area | Existing behavior and implication |
|---|---|
| Titles | Parser dispatch recognizes OSC 0/2 as `SetWindowTitle`. `Terminal::SetWindowTitle` updates/notifies unless application titles are suppressed; `TermControl` bubbles title changes. Consume accepted titles as optional metadata, respecting suppression, not as task identity or universal step completion. [Parser][titles-parser], [Terminal API][titles-api], [control event][titles-control]. |
| Progress | `AdaptDispatch::DoConEmuAction` handles OSC 9;4 and calls `SetTaskbarProgress`. Terminal stores state/value, but clear resets the value, indeterminate preserves it, and error/paused-style zero values may retain/synthesize progress. Upstream also clamps excessive percentages, whereas this demo rejects values outside 0..100. Preserve state and value separately; do not equate styling with lifecycle. [Dispatch][progress-dispatch], [storage][progress-api]. |
| Pane vs tab | `Tab` registers title/progress handlers with 200 ms throttling. `GetCombinedTaskbarState` picks a priority state across panes. Attach before this aggregation: it loses the identity needed for separate Shell tasks. [Handlers/aggregation][tab-progress], [throttle][tab-throttle]. |
| Lifecycle | OSC 133 A/B/C/D calls `StartPrompt`, `StartCommand`, `StartOutput`, and `EndCurrentCommand`. B is input, C is execution. D parses an optional unsigned exit code; omitted is `nullopt`, malformed/negative supplied values become `UINT_MAX`. The buffer/row actually stores that optional code, not merely the parser token. [Dispatch][lifecycle-dispatch], [buffer][lifecycle-buffer], [row][lifecycle-row]. |
| Outcome caveat | `ControlCore::ReadLastPrompt` regards an exit code being present as completion, so D without a code is not recognized as completed there. `NotifyShellIntegrationMark` triggers scroll notification, not a typed finish event. A bridge must preserve an explicit finish-with-unknown-outcome event independently of `ReadLastPrompt`. [Reader][last-prompt], [notification][mark-notification]. |
| Agent event path | `TerminalPage::_RegisterTerminalEvents` receives `VtSequenceReceived` and marshals from the connection-reader thread to UI. Its existing forwarding filter accepts `AgentEvent;...` and `osc:133;...`, not title/progress. OSC 133 forwarding is also subject to auto-fix and shell-access/error-detection policy. This is a useful nearby registration point, not a ready-made complete task feed. [Registration][vt-registration], [filter/policy][vt-filter], [forwarding][vt-forward]. |
| Identity | Tabs have `StableId` values and assign numeric pane IDs. Protocol pane identity uses connection SessionId GUIDs, while protocol tab IDs are indexes. Terminal executable `focus-pane -t` expects a numeric pane ID; WTA's `wtcli focus-pane -t` uses a connection GUID. Do not confuse these surfaces or persist indexes as durable task routes. [Tab creation][tab-id], [protocol IDs][protocol-ids], [executable focus syntax][focus-args], [WTA model][wta-model]. |
| Activation | `TerminalProtocolComServer::FocusPane(GUID)` searches windows and invokes `TerminalPage::FocusProtocolPane`. That route summons the window, selects the tab, focuses the pane, and can restore stashed/hidden agent panes. Resolve a host-owned opaque task ID to this live route; never derive a shell command from card/output text. [COM route][focus-com], [page route][focus-page], [hidden-agent restoration][focus-hidden]. |

The scrollbar-mark feature is configured as AlwaysEnabled except for
WindowsInbox branding in [features.xml][features]. Branding/configuration still
matters when deciding which lifecycle surface is available.

## Packaging and activation gaps

The release manifest declares `Microsoft.IntelligentTerminal`, application `App`,
execution aliases, and packaged COM registration for `TerminalProtocolComServer`
([identity][package-id], [extensions][package-extensions]).
`WindowEmperor` partitions single-instance routing by branding, elevation,
user SID, and, for unpackaged instances, install path ([routing][window-routing]).
An unpackaged AUMID is not package identity, and elevated/normal windows are not
necessarily one singleton.

**No existing AppTask provider was found in the inspected scope.** The pinned
recursive tree had no AppTask/TaskInfo-named paths; the four inspected package
manifests had no AppTaskInfo/AppTaskProvider or `windows.protocol` matches.
Supplementary GitHub indexed `AppTaskInfo` search returned no results, but that
search was not commit-pinned. This is a bounded negative finding, not proof that
every possible wrapper or runtime path is absent.

A real integration therefore still needs a packaged provider extension, SDK/API
availability checks, and an explicitly registered safe deep-link entry point.
Existing COM focus support is not itself a Shell URI handler. Prefer publishing
from the packaged app rather than assuming a separate agent/helper has its identity.
The target SDK and AppTask contract build compatibility were not validated in this
research.

## Recommended first implementation

```text
Accepted pane title/progress + explicit OSC 133 transitions
    -> pane/connection-generation adapter
    -> independent task registry and lifecycle
    -> packaged AppTaskInfo publisher

Shell activation -> opaque host task ID -> current connection GUID
                 -> existing cross-window FocusPane route
```

Keep one active shell-command task per connection generation initially. C starts
execution; D carries a nullable outcome. Ignore repeated terminal transitions.
Progress/clear/100%/warning are not lifecycle authority. Title publication and
title-to-step inference must remain separate opt-ins; cwd/command titles are not
safe activity labels by default.

Use accepted title/progress control events plus explicit lifecycle notifications,
not an aggregated taskbar state or scrollback scraping. Consider a small typed
notification at the dispatch/Core boundary so omitted D outcomes survive. Honor
the host's privacy settings; a Shell-task option must not silently bypass existing
"don't access my shell" policy just because an earlier VT event is available.

Reuse the demo's semantics and test cases, not its process-output UI. Upstream's
native C++ TerminalApp/Core and Rust WTA are not a drop-in host for these C#
classes. Port or adapt the small lifecycle/publisher boundary deliberately; do not
insert a second byte parser unnecessarily when native dispatch already provides
the events. Keep parser, lifecycle, publisher, and presentation separate.

## Persistence, trust, and unresolved cases

- Mint durable host task IDs independently of transient pane GUIDs. Store a
  revocable route mapping; closed/moved/restarted connections may invalidate it.
  Missing routes should open a truthful recovery view, never replay stored commands.
- The code captures SessionId before teardown to avoid lost close notifications,
  and forwards connection closed/failed events independently of auto-fix settings
  ([close capture][close-capture], [connection state][connection-state]).
  `GetProcessStatus` reports the connection root-process exit status, **not**
  necessarily the last shell-command exit code ([process status][process-status]).
- COM event delivery is bounded/drop-oldest ([event queue][event-queue]).
  Event consumption alone is not durable truth. Use tombstones and live-pane
  reconciliation; WTA's registry already demonstrates refusing to resurrect
  locally ended sessions from stale broadcasts ([reconciliation][wta-reconcile]).
- Respect Shell `HiddenByUser` and task removal. A hidden Shell card is not a
  closed pane, and an agent pane stashed by the terminal is not necessarily ended.
  Stale persisted Running cards require explicit reconciliation/cleanup policy.
- Existing forwarding replaces a payload's `pane_id` with the originating
  connection identity ([binding][pane-binding]). That identifies the stream,
  not the particular process/remote job writing inside it. Nested shells, SSH,
  tmux and concurrent background jobs remain ambiguous without additional policy.
- `Authenticate` ignores its token and describes COM activation as its boundary
  ([handshake][authenticate]). Do not mistake this compatibility handshake for
  payload authentication. ACL/elevation behavior needs its own investigation.
- Preserve bounded text/history/rate limits, sanitization and safe action routes.
  Do not publish command text, stdout, cwd, asset paths, or private labels by default.

Before claiming end-to-end support, test malformed/omitted D codes, dropped events,
pane transfer, hidden non-agent panes, elevated/normal windows, cold activation,
restart recovery, and actual Shell cards on supported Windows. No custom OSC
namespace or JSON extension is required for the first phase.

See [rich task-card design](rich-osc-task-cards.md) for the implemented demo
convention versus the still-proposed negotiated metadata extension, and the
[README](../README.md) for current demo verification limits.

[titles-parser]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/terminal/parser/OutputStateMachineEngine.cpp#L776-L784
[titles-api]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalCore/TerminalApi.cpp#L91-L102
[titles-control]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalControl/TermControl.cpp#L322-L331
[progress-dispatch]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/terminal/adapter/adaptDispatch.cpp#L3574-L3606
[progress-api]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalCore/TerminalApi.cpp#L174-L215
[tab-progress]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/Tab.cpp#L1184-L1227
[tab-throttle]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/Tab.cpp#L1355-L1365
[lifecycle-dispatch]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/terminal/adapter/adaptDispatch.cpp#L3692-L3747
[lifecycle-buffer]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/buffer/out/textBuffer.cpp#L3486-L3514
[lifecycle-row]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/buffer/out/Row.cpp#L1278-L1286
[last-prompt]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalControl/ControlCore.cpp#L2550-L2583
[mark-notification]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalCore/TerminalApi.cpp#L460-L463
[vt-registration]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.cpp#L8661-L8690
[vt-filter]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.cpp#L8708-L8733
[vt-forward]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.cpp#L8802-L8837
[tab-id]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/Tab.cpp#L35-L51
[protocol-ids]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.Protocol.cpp#L151-L173
[focus-args]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/AppCommandlineArgs.cpp#L511-L534
[wta-model]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/tools/wta/src/agent_sessions.rs#L7-L26
[focus-com]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/WindowsTerminal/TerminalProtocolComServer.cpp#L1060-L1076
[focus-page]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.Protocol.cpp#L916-L955
[focus-hidden]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.Protocol.cpp#L957-L1007
[features]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/features.xml#L96-L103
[package-id]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/CascadiaPackage/Package.appxmanifest#L21-L24
[package-extensions]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/CascadiaPackage/Package.appxmanifest#L199-L217
[window-routing]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/WindowsTerminal/WindowEmperor.cpp#L542-L608
[close-capture]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.cpp#L8878-L8900
[connection-state]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.cpp#L8912-L8934
[process-status]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.Protocol.cpp#L606-L629
[event-queue]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/WindowsTerminal/TerminalProtocolComServer.cpp#L400-L422
[wta-reconcile]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/tools/wta/src/agent_sessions.rs#L1179-L1218
[pane-binding]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/TerminalApp/TerminalPage.cpp#L8752-L8782
[authenticate]: https://github.com/microsoft/intelligent-terminal/blob/8ba6fdef80b80acabb4ff269b1c0aab9e9970c55/src/cascadia/WindowsTerminal/TerminalProtocolComServer.cpp#L511-L524
