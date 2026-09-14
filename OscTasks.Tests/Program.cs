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
if (args.Length != 1) throw new ArgumentException("Pass the built agent DLL path for required real-process integration tests.");
foreach (var (scenario, expected) in new[]
{
    ("success", RunState.Completed), ("failure", RunState.Error), ("indeterminate", RunState.Completed),
    ("warning", RunState.Completed), ("unknown", RunState.Unknown), ("crash", RunState.Error)
})
{
    var life = new TaskLifecycle();
    bool sawIndeterminate = false;
    var result = await AgentProcess.RunAsync("dotnet", [Path.GetFullPath(args[0]), scenario, "--fast"], e =>
    {
        life.Accept(e);
        sawIndeterminate |= life.Snapshot.IsIndeterminate;
    }, _ => { }, CancellationToken.None);
    life.Disconnect(result.ExitCode, result.WasCancelled);
    Check(life.Snapshot.State == expected && life.TerminalTransitions == 1, $"real agent bytes -> parser -> lifecycle: {scenario}");
    if (scenario == "indeterminate") Check(sawIndeterminate, "indeterminate progress observed");
}
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
