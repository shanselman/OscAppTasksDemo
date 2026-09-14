namespace OscTasks.Core;

public enum RunState { Waiting, Running, Completed, Error, Unknown, Cancelled }

public sealed record TaskSnapshot(RunState State, int? Percent, bool IsIndeterminate, string Label, string Title)
{
    public bool IsTerminal => State is RunState.Completed or RunState.Error or RunState.Unknown or RunState.Cancelled;
}

/// <summary>One invocation per instance. OSC progress is never completion authority.</summary>
public sealed class TaskLifecycle
{
    public TaskSnapshot Snapshot { get; private set; } = new(RunState.Waiting, null, false, "Waiting for OSC 133;C", "");
    public int TerminalTransitions { get; private set; }

    public void Accept(StreamEvent e)
    {
        if (Snapshot.IsTerminal) return;
        if (e.Kind == EventKind.Title) { Snapshot = Snapshot with { Title = e.Detail }; return; }
        if (e.Kind == EventKind.Execute && Snapshot.State == RunState.Waiting)
            Snapshot = Snapshot with { State = RunState.Running, Label = "Command execution began" };
        if (Snapshot.State != RunState.Running) return;
        if (e.Kind == EventKind.Progress)
            Snapshot = Snapshot with
            {
                Percent = e.Value is 0 or 3 ? null : e.Percent,
                IsIndeterminate = e.Value == 3,
                Label = e.Value switch
                {
                    0 => "Progress cleared; still awaiting command outcome",
                    1 => $"{e.Percent}% - still running, even at 100%",
                    2 => $"{e.Percent}% - error-colored progress; outcome not yet known",
                    3 => "Working - progress is indeterminate",
                    4 => $"{e.Percent}% - warning; does not imply pause or input required",
                    _ => Snapshot.Label
                }
            };
        if (e.Kind == EventKind.Finish)
            Finish(e.Value switch { 0 => RunState.Completed, null => RunState.Unknown, _ => RunState.Error },
                e.Value switch { 0 => "Completed: OSC 133;D;0", null => "Command ended without an exit code; outcome unknown", _ => $"Failed: OSC exit code {e.Value}" });
    }

    public void Disconnect(int? exitCode, bool cancelled = false)
    {
        if (Snapshot.IsTerminal) return;
        Finish(cancelled ? RunState.Cancelled : exitCode is not null and not 0 ? RunState.Error : RunState.Unknown,
            cancelled ? "Cancelled by host; work is not resumable" :
            exitCode is not null and not 0 ? $"Process exited {exitCode} without a terminal marker" :
            "Stream ended without an authoritative command outcome");
    }

    private void Finish(RunState state, string label)
    {
        Snapshot = Snapshot with { State = state, IsIndeterminate = false, Label = label };
        TerminalTransitions++;
    }
}
