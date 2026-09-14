using System.Collections.Immutable;

namespace OscTasks.Core;

public enum RunState { Waiting, Running, Completed, Error, Unknown, Cancelled }

public sealed record TaskSnapshot(RunState State, int? Percent, bool IsIndeterminate, string Label, string Title)
{
    public bool IsTerminal => State is RunState.Completed or RunState.Error or RunState.Unknown or RunState.Cancelled;
    public string CurrentActivity { get; init; } = "";
    public bool IsTitleStepHistoryEnabled { get; init; }
    public ImmutableArray<string> CompletedActivities { get; init; } = [];
    public int DroppedActivities { get; init; }
    public string ActivityStatus => CurrentActivity.Length == 0 ? Label :
        IsTerminal ? $"{CurrentActivity} / {State}" :
        IsIndeterminate ? $"{CurrentActivity} -- indeterminate" :
        Percent is int percent ? $"{CurrentActivity} -- {percent}%" : CurrentActivity;
    public string ExecutingLabel => CurrentActivity.Length == 0 ? Label : $"{CurrentActivity} -- {Label}";
    public string StepHistory => !IsTitleStepHistoryEnabled ? "Title-to-step history OFF" :
        "OPT-IN title steps (not standard OSC semantics)\n" +
        (DroppedActivities > 0 ? $"[{DroppedActivities} older completed activities omitted]\n" : "") +
        $"Completed: {(CompletedActivities.IsEmpty ? "(none)" : string.Join("; ", CompletedActivities))}";
    public string Summary => Label +
        (IsTitleStepHistoryEnabled ? $"\n{StepHistory}" : "") +
        (CurrentActivity.Length == 0 ? "" :
            $"\n{(State == RunState.Completed ? "Last activity" : "Unfinished activity")}: {CurrentActivity}");
}

/// <summary>One invocation per instance. OSC progress is never completion authority.</summary>
public sealed class TaskLifecycle
{
    public const int MaxCompletedActivities = 8;
    private readonly bool _useTitleAsActivity;
    public TaskSnapshot Snapshot { get; private set; }
    public int TerminalTransitions { get; private set; }

    public TaskLifecycle(bool useTitleAsActivity = false, bool enableTitleStepHistory = false)
    {
        if (enableTitleStepHistory && !useTitleAsActivity)
            throw new ArgumentException("Title steps require explicit title-as-activity opt-in.", nameof(enableTitleStepHistory));
        _useTitleAsActivity = useTitleAsActivity;
        Snapshot = new(RunState.Waiting, null, false, "Waiting for OSC 133;C", "")
        {
            IsTitleStepHistoryEnabled = enableTitleStepHistory
        };
    }

    public void Accept(StreamEvent e)
    {
        if (Snapshot.IsTerminal) return;
        if (e.Kind == EventKind.Title)
        {
            string title = OscParser.SafeText(e.Detail, 120).Trim();
            Snapshot = Snapshot with { Title = title };
            // Pre-execution titles may be shell branding/cwd. Only in-command titles are activities.
            if (_useTitleAsActivity && Snapshot.State == RunState.Running &&
                title.Length > 0 && title != Snapshot.CurrentActivity)
            {
                CompleteCurrentActivity();
                Snapshot = Snapshot with { CurrentActivity = title };
            }
            return;
        }
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
        if (state == RunState.Completed)
        {
            CompleteCurrentActivity();
            if (Snapshot.IsTitleStepHistoryEnabled)
                Snapshot = Snapshot with { CurrentActivity = "" };
        }
        Snapshot = Snapshot with { State = state, IsIndeterminate = false, Label = label };
        TerminalTransitions++;
    }

    private void CompleteCurrentActivity()
    {
        if (!Snapshot.IsTitleStepHistoryEnabled || Snapshot.CurrentActivity.Length == 0) return;
        var completed = Snapshot.CompletedActivities.Add(Snapshot.CurrentActivity);
        bool trim = completed.Length > MaxCompletedActivities;
        Snapshot = Snapshot with
        {
            CompletedActivities = trim ? completed.RemoveAt(0) : completed,
            DroppedActivities = Snapshot.DroppedActivities + (trim ? 1 : 0)
        };
    }
}
