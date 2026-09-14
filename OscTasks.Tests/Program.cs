using System.Text;
using OscTasks.Core;

int passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
    passed++;
}
List<StreamEvent> Parse(byte[] bytes, int chunk = 1)
{
    var result = new List<StreamEvent>();
    var parser = new OscParser(result.Add);
    for (int i = 0; i < bytes.Length; i += chunk)
        parser.Feed(bytes.AsSpan(i, Math.Min(chunk, bytes.Length - i)));
    parser.Complete();
    return result;
}
string wire = "café🙂\x1b]2;title\x1b\\\x1b]133;A\a\x1b]133;B\a\x1b]133;C\a\x1b]9;4;1;100\a\x1b]9;4;0;0\a\x1b]133;D;0\x1b\\end";
byte[] input = Encoding.UTF8.GetBytes(wire);
for (int split = 0; split <= input.Length; split++)
{
    var events = new List<StreamEvent>();
    var parser = new OscParser(events.Add);
    parser.Feed(input.AsSpan(0, split));
    parser.Feed(input.AsSpan(split));
    parser.Complete();
    Check(string.Concat(events.Where(e => e.Kind == EventKind.Text).Select(e => e.Detail)) == "café🙂end" &&
          events.Count(e => e.Kind == EventKind.Finish && e.Value == 0) == 1 &&
          events.Single(e => e.Kind == EventKind.Title).Detail == "title" &&
          events.All(e => e.Kind != EventKind.Invalid), $"UTF-8/BEL/ST split at byte {split}");
}
Check(Parse(input).Count(e => e.Kind == EventKind.Title) == 1, "one-byte chunks");
var malformed = Parse(Encoding.UTF8.GetBytes("\x1b]9;4;1;101\a\x1b]133;D;no\a\x1b]999;noop\a\x1b]2;bad\x1bXdiscard\aok"));
Check(malformed.Count(e => e.Kind == EventKind.Invalid) == 3 && malformed.Count(e => e.Kind == EventKind.Unknown) == 1, "malformed and unknown OSC");
Check(string.Concat(malformed.Where(e => e.Kind == EventKind.Text).Select(e => e.Detail)) == "ok", "malformed OSC payload never leaks into output");
var oversized = Parse(Encoding.UTF8.GetBytes("\x1b]2;" + new string('x', 100000) + "\x1b\\after"));
Check(oversized.Count(e => e.Kind == EventKind.Invalid) == 1 &&
      string.Concat(oversized.Where(e => e.Kind == EventKind.Text).Select(e => e.Detail)) == "after", "oversized sequence bounded and recovers");
Check(Parse(Encoding.UTF8.GetBytes("\x1b]133;D")).Last().Kind == EventKind.Invalid, "truncated OSC at EOF");
Check(Parse([0xc3]).Any(e => e.Kind == EventKind.Invalid), "truncated UTF-8 reported");
Check(Parse(Encoding.UTF8.GetBytes("\ufffd")).All(e => e.Kind == EventKind.Text), "literal replacement character is valid UTF-8");
Check(Parse([0xff]).Any(e => e.Kind == EventKind.Invalid), "malformed UTF-8 reported by decoder");
Check(Parse(Encoding.UTF8.GetBytes("\x1b]9;4;+1;50\a")).Single().Kind == EventKind.Invalid, "progress state uses strict invariant digits");
Check(Parse(Encoding.UTF8.GetBytes("\x1b")).Single().Kind == EventKind.Invalid, "truncated escape at EOF");
foreach (var progress in new[] { new StreamEvent(EventKind.Progress, "", 1, 100), new StreamEvent(EventKind.Progress, "", 0, 0),
    new StreamEvent(EventKind.Progress, "", 4, 50), new StreamEvent(EventKind.Progress, "", 2, 60) })
{
    var life = new TaskLifecycle();
    life.Accept(new(EventKind.Input, ""));
    Check(life.Snapshot.State == RunState.Waiting, "OSC B is input, not execution");
    life.Accept(new(EventKind.Execute, ""));
    life.Accept(progress);
    Check(life.Snapshot.State == RunState.Running, $"progress {progress.Value} is not completion, pause or needs-attention");
    life.Accept(new(EventKind.Finish, "", 0));
    life.Accept(new(EventKind.Finish, "", 1));
    life.Disconnect(7);
    Check(life.Snapshot.State == RunState.Completed && life.TerminalTransitions == 1, "terminal transition idempotent");
}
var unknown = new TaskLifecycle();
unknown.Accept(new(EventKind.Execute, ""));
unknown.Accept(new(EventKind.Finish, ""));
unknown.Disconnect(0);
Check(unknown.Snapshot.State == RunState.Unknown, "missing exit code stays unknown");
foreach (int? exit in new int?[] { null, 0, 7 })
{
    var life = new TaskLifecycle();
    life.Accept(new(EventKind.Execute, ""));
    life.Disconnect(exit);
    Check(life.Snapshot.State == (exit == 7 ? RunState.Error : RunState.Unknown), $"disconnect exit {exit}");
}
var arbitrarySource = new TaskLifecycle();
arbitrarySource.Accept(new(EventKind.Execute, ""));
arbitrarySource.Accept(new(EventKind.Title, "private working directory"));
Check(arbitrarySource.Snapshot.Title == "private working directory" &&
      arbitrarySource.Snapshot.CurrentActivity == "" && arbitrarySource.Snapshot.CompletedActivities.IsEmpty,
      "arbitrary sources keep title publication and step inference off by default");
bool rejectedImplicitPublication = false;
try { _ = new TaskLifecycle(enableTitleStepHistory: true); }
catch (ArgumentException) { rejectedImplicitPublication = true; }
Check(rejectedImplicitPublication, "step history requires explicit activity publication consent");

var activityOnly = new TaskLifecycle(useTitleAsActivity: true);
activityOnly.Accept(new(EventKind.Execute, ""));
activityOnly.Accept(new(EventKind.Title, "Inspecting project"));
activityOnly.Accept(new(EventKind.Title, "Running tests"));
activityOnly.Accept(new(EventKind.Progress, "", 1, 65));
Check(activityOnly.Snapshot.ActivityStatus == "Running tests -- 65%" &&
      activityOnly.Snapshot.CompletedActivities.IsEmpty, "activity plus percentage does not imply step history");
activityOnly.Accept(new(EventKind.Finish, "", 0));
Check(activityOnly.Snapshot.CompletedActivities.IsEmpty &&
      activityOnly.Snapshot.Summary.Contains("Last activity: Running tests"), "history stays off even after success");

var steps = new TaskLifecycle(useTitleAsActivity: true, enableTitleStepHistory: true);
steps.Accept(new(EventKind.Title, "Shell branding"));
steps.Accept(new(EventKind.Progress, "", 1, 80));
steps.Accept(new(EventKind.Execute, ""));
Check(steps.Snapshot.CurrentActivity == "" && steps.Snapshot.Percent is null,
    "pre-execution metadata/progress cannot create completed activities");
steps.Accept(new(EventKind.Title, "Inspecting project"));
steps.Accept(new(EventKind.Title, "Inspecting project"));
steps.Accept(new(EventKind.Title, "  "));
Check(steps.Snapshot.CompletedActivities.IsEmpty && steps.Snapshot.CurrentActivity == "Inspecting project",
    "consecutive repeated/blank titles do not invent steps");
steps.Accept(new(EventKind.Title, "Editing files"));
var savedSnapshot = steps.Snapshot;
steps.Accept(new(EventKind.Title, "Running tests"));
var beforeDuplicateStart = steps.Snapshot;
steps.Accept(new(EventKind.Execute, ""));
Check(steps.Snapshot == beforeDuplicateStart, "duplicate execution marker cannot reset an active invocation");
steps.Accept(new(EventKind.Progress, "", 1, 100));
steps.Accept(new(EventKind.Progress, "", 0, 0));
Check(steps.Snapshot.State == RunState.Running &&
      steps.Snapshot.CompletedActivities.SequenceEqual(new[] { "Inspecting project", "Editing files" }) &&
      steps.Snapshot.CurrentActivity == "Running tests", "100 and clear do not complete current activity");
steps.Accept(new(EventKind.Text, "Everything succeeded!"));
Check(steps.Snapshot.State == RunState.Running, "narrative is not lifecycle authority");
steps.Accept(new(EventKind.Finish, "", 0));
var terminalSnapshot = steps.Snapshot;
steps.Accept(new(EventKind.Title, "Restored shell title"));
steps.Accept(new(EventKind.Finish, "", 1));
steps.Disconnect(1);
Check(steps.Snapshot == terminalSnapshot && steps.TerminalTransitions == 1 &&
      steps.Snapshot.CompletedActivities.SequenceEqual(new[] { "Inspecting project", "Editing files", "Running tests" }) &&
      steps.Snapshot.CurrentActivity == "", "explicit success completes the final opt-in activity once");
Check(savedSnapshot.CompletedActivities.SequenceEqual(new[] { "Inspecting project" }),
    "published snapshots retain immutable activity history");
foreach (string outcome in new[] { "failure", "unknown", "cancel", "disconnect" })
{
    var failedSteps = new TaskLifecycle(true, true);
    failedSteps.Accept(new(EventKind.Execute, ""));
    failedSteps.Accept(new(EventKind.Title, "Inspecting project"));
    failedSteps.Accept(new(EventKind.Title, "Running tests"));
    if (outcome == "failure") failedSteps.Accept(new(EventKind.Finish, "", 1));
    else if (outcome == "unknown") failedSteps.Accept(new(EventKind.Finish, ""));
    else failedSteps.Disconnect(null, outcome == "cancel");
    Check(failedSteps.Snapshot.CompletedActivities.SequenceEqual(new[] { "Inspecting project" }) &&
          failedSteps.Snapshot.CurrentActivity == "Running tests" &&
          failedSteps.Snapshot.Summary.Contains("Unfinished activity: Running tests"),
          $"{outcome} does not mark the interrupted activity completed");
}
var boundedSteps = new TaskLifecycle(true, true);
boundedSteps.Accept(new(EventKind.Execute, ""));
for (int i = 0; i < 12; i++) boundedSteps.Accept(new(EventKind.Title, $"Step {i}"));
boundedSteps.Accept(new(EventKind.Finish, "", 0));
Check(boundedSteps.Snapshot.CompletedActivities.Length == TaskLifecycle.MaxCompletedActivities &&
      boundedSteps.Snapshot.CompletedActivities[0] == "Step 4" && boundedSteps.Snapshot.DroppedActivities == 4 &&
      boundedSteps.Snapshot.Summary.Contains("4 older completed activities omitted"), "history retention is bounded and truncation visible");
var replaySteps = new TaskLifecycle(true, true);
Check(replaySteps.Snapshot.State == RunState.Waiting && replaySteps.Snapshot.CurrentActivity == "" &&
      replaySteps.Snapshot.CompletedActivities.IsEmpty && replaySteps.Snapshot.DroppedActivities == 0 &&
      replaySteps.Snapshot.Percent is null, "new run/reset has no stale activity, progress or history");
replaySteps.Accept(new(EventKind.Execute, ""));
replaySteps.Accept(new(EventKind.Title, "Safe\u202e\u0001" + new string('x', 150)));
Check(replaySteps.Snapshot.CurrentActivity.Length == 120 &&
      !replaySteps.Snapshot.CurrentActivity.Contains('\u202e') &&
      !replaySteps.Snapshot.CurrentActivity.Contains('\u0001'), "activity labels are sanitized and bounded");

if (args.Length != 1) throw new ArgumentException("Pass the built agent DLL path for required real-process integration tests.");
foreach (var (scenario, expected) in new[]
{
    ("success", RunState.Completed), ("failure", RunState.Error), ("indeterminate", RunState.Completed),
    ("warning", RunState.Completed), ("unknown", RunState.Unknown), ("crash", RunState.Error)
})
{
    var life = new TaskLifecycle(useTitleAsActivity: true, enableTitleStepHistory: true);
    var noHistory = new TaskLifecycle(useTitleAsActivity: true);
    bool sawIndeterminate = false;
    bool sawRichProgress = false;
    var result = await AgentProcess.RunAsync("dotnet", [Path.GetFullPath(args[0]), scenario, "--fast", "--synthetic-shell-markers"], e =>
    {
        life.Accept(e);
        noHistory.Accept(e);
        sawIndeterminate |= life.Snapshot.IsIndeterminate;
        sawRichProgress |= life.Snapshot.ActivityStatus == "Running tests -- 65%" &&
            life.Snapshot.CompletedActivities.SequenceEqual(new[] { "Inspecting project", "Editing files" });
    }, _ => { }, CancellationToken.None);
    life.Disconnect(result.ExitCode, result.WasCancelled);
    noHistory.Disconnect(result.ExitCode, result.WasCancelled);
    Check(life.Snapshot.State == expected && life.TerminalTransitions == 1, $"real agent bytes -> parser -> lifecycle: {scenario}");
    Check(noHistory.Snapshot.CompletedActivities.IsEmpty && noHistory.Snapshot.State == expected,
        $"real agent respects history opt-out: {scenario}");
    if (scenario != "crash") Check(sawRichProgress, $"ordered rich activity/65% snapshot: {scenario}");
    Check(expected == RunState.Completed
        ? life.Snapshot.CompletedActivities.SequenceEqual(new[] { "Inspecting project", "Editing files", "Running tests" })
        : life.Snapshot.CurrentActivity == (scenario == "crash" ? "Inspecting project" : "Running tests") &&
          !life.Snapshot.CompletedActivities.Contains(life.Snapshot.CurrentActivity),
        $"real terminal activity history: {scenario}");
    if (scenario == "indeterminate") Check(sawIndeterminate, "indeterminate progress observed");
}
foreach (string scenario in new[] { "success", "failure", "indeterminate", "warning", "unknown", "crash" })
{
    var shellOwned = new TaskLifecycle(true, true);
    var noShell = new TaskLifecycle(true, true);
    int lifecycleMarkers = 0;
    var titles = new List<string>();
    shellOwned.Accept(new(EventKind.Execute, "Test harness simulates the surrounding shell's C"));
    var result = await AgentProcess.RunAsync("dotnet", [Path.GetFullPath(args[0]), scenario, "--fast"], e =>
    {
        shellOwned.Accept(e);
        noShell.Accept(e);
        if (e.Kind is EventKind.Prompt or EventKind.Input or EventKind.Execute or EventKind.Finish) lifecycleMarkers++;
        if (e.Kind == EventKind.Title) titles.Add(e.Detail);
    }, _ => { }, CancellationToken.None);
    Check(lifecycleMarkers == 0, $"normal agent emits no shell-owned markers: {scenario}");
    Check(titles.SequenceEqual(scenario == "crash"
        ? new[] { "Inspecting project" }
        : new[] { "Inspecting project", "Editing files", "Running tests", "Running tests" }),
        $"normal agent preserves real activity titles: {scenario}");
    Check(!shellOwned.Snapshot.IsTerminal && noShell.Snapshot.State == RunState.Waiting,
        $"agent output alone cannot finish or start a shell command: {scenario}");
    shellOwned.Accept(new(EventKind.Finish, "Test harness supplies shell D using the real exit code", result.ExitCode));
    shellOwned.Disconnect(result.ExitCode);
    Check(shellOwned.TerminalTransitions == 1 &&
          shellOwned.Snapshot.State == (result.ExitCode == 0 ? RunState.Completed : RunState.Error),
          $"shell-owned lifecycle composes with normal agent exit: {scenario}");
}
var invalidArguments = await AgentProcess.RunAsync("dotnet",
    [Path.GetFullPath(args[0]), "--not-a-real-option"], _ => { }, _ => { }, CancellationToken.None);
Check(invalidArguments.ExitCode == 64, "unknown CLI options are rejected instead of silently changing fixture behavior");
using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(400)))
{
    var life = new TaskLifecycle();
    var result = await AgentProcess.RunAsync("dotnet", [Path.GetFullPath(args[0]), "success"], life.Accept, _ => { }, cancel.Token);
    life.Disconnect(result.ExitCode, result.WasCancelled);
    Check(result.WasCancelled && life.Snapshot.State == RunState.Cancelled, "cancellation kills/reaps child and produces one terminal state");
}
using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
{
    bool consumerErrorObserved = false;
    try
    {
        await AgentProcess.RunAsync("dotnet", [Path.GetFullPath(args[0]), "success"],
            _ => throw new IOException("test consumer failure"), _ => { }, deadline.Token);
    }
    catch (IOException ex) when (ex.Message == "test consumer failure")
    {
        consumerErrorObserved = true;
    }
    Check(consumerErrorObserved && !deadline.IsCancellationRequested, "stream consumer failure is propagated and child is reaped");
}
Console.WriteLine($"All {passed} assertions passed.");
