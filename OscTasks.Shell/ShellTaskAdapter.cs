using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.UI.Shell.Tasks;
using OscTasks.Core;

namespace OscTasks.Shell;

public sealed record ShellStatus(bool IsSupported, bool IsFailure, string Message);

/// <summary>Packaged WinRT boundary, independent of the demo UI and process transport.</summary>
public sealed class ShellTaskAdapter
{
    private WindowsTaskBackend? _backend;
    public ShellStatus Status { get; private set; } = new(false, false, "Not probed");
    public string Identity { get; private set; } = "";
    public string LastResult { get; private set; } = "No task API invocation";

    public ShellTaskAdapter()
    {
        try
        {
            Identity = Package.Current.Id.FullName;
            if (!ApiInformation.IsTypePresent("Windows.UI.Shell.Tasks.AppTaskInfo"))
            {
                Status = new(false, false, $"Unsupported: AppTaskInfo runtime type is absent. OS {Environment.OSVersion.Version}");
                return;
            }
            if (!ApiInformation.IsMethodPresent("Windows.UI.Shell.Tasks.AppTaskInfo", "IsSupported"))
            {
                Status = new(false, false, "Unsupported: AppTaskInfo.IsSupported is absent.");
                return;
            }
            InitializeSupportedBackend();
        }
        catch (Exception ex) when (IsApiError(ex)) { Fail("Capability/identity probe", ex); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeSupportedBackend()
    {
        if (!AppTaskInfo.IsSupported())
        {
            Status = new(false, false, $"Unsupported: AppTaskInfo.IsSupported() returned false. OS {Environment.OSVersion.Version}; feature rollout not enabled.");
            return;
        }
        _backend = new WindowsTaskBackend();
        Status = new(true, false, "Supported: packaged identity and AppTaskInfo.IsSupported() = true. API success is not proof of a visible Shell card.");
        LastResult = _backend.DescribeExisting();
    }

    public void Begin(Guid sessionId)
    {
        if (_backend is null) return;
        _backend.Begin(sessionId);
        Status = new(true, false, "AppTaskInfo.IsSupported() = true. New run; real API results will appear below.");
        LastResult = "No task created for this run yet. Waiting for command execution.";
    }

    public void Publish(TaskSnapshot snapshot, string scenario)
    {
        if (_backend is null || Status.IsFailure || snapshot.State == RunState.Waiting) return;
        try { LastResult = _backend.Publish(snapshot, scenario); }
        catch (Exception ex) when (IsApiError(ex)) { Fail("Create/Update", ex); }
    }

    public void ClearOwnedTasks()
    {
        if (_backend is null) return;
        try
        {
            LastResult = _backend.ClearOwnedTasks();
            Status = new(true, false, "Supported: demo-owned tasks cleared through the real API.");
        }
        catch (Exception ex) when (IsApiError(ex)) { Fail("FindAll/Remove", ex); }
    }

    public string InspectRoute(Uri uri)
    {
        if (!IsSafeRoute(uri)) return "Rejected activation: only osctasksdemo://task/<GUID> is accepted.";
        if (_backend is null) return $"Opened demo route {uri.AbsolutePath}; Shell API unavailable.";
        try { return _backend.InspectRoute(uri); }
        catch (Exception ex) when (IsApiError(ex)) { Fail("FindAll", ex); return LastResult; }
    }

    public static bool IsSafeRoute(Uri uri) =>
        uri.IsAbsoluteUri && uri.Scheme == "osctasksdemo" && uri.Host == "task" &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort &&
        uri.AbsolutePath.Length == 37 && Guid.TryParseExact(uri.AbsolutePath[1..], "D", out _);

    private static bool IsApiError(Exception ex) => ex is COMException or InvalidOperationException
        or ArgumentException or TypeLoadException or MissingMethodException or NotSupportedException
        or UnauthorizedAccessException;

    private void Fail(string operation, Exception ex)
    {
        LastResult = $"{operation} FAILED: {ex.GetType().Name} 0x{ex.HResult:X8}: {ex.Message}";
        Status = new(_backend is not null, true, LastResult);
    }
}

internal sealed class WindowsTaskBackend
{
    private const string GroupTitle = "OSC App Tasks Demo";
    private AppTaskInfo? _task;
    private Guid _session;
    private bool _suppressed;

    public void Begin(Guid sessionId) { _session = sessionId; _task = null; _suppressed = false; }

    private static bool IsOwned(AppTaskInfo task) =>
        task.Title == GroupTitle && ShellTaskAdapter.IsSafeRoute(task.DeepLink);

    public string DescribeExisting()
    {
        var all = AppTaskInfo.FindAll();
        if (all is null)
            return "FindAll returned null, not a task collection. Existing tasks could not be inspected.\n" +
                "The capability probe succeeded; a new run will independently attempt Create.";
        var tasks = all.Where(IsOwned).ToArray();
        return $"FindAll succeeded: {tasks.Length} persisted demo task(s), {tasks.Count(t => t.HiddenByUser)} hidden.\n" +
            "Previous tasks are not resumed or recreated. Clear demo tasks removes only this provider's demo routes.";
    }

    public string Publish(TaskSnapshot snapshot, string scenario)
    {
        if (_suppressed) return "Shell task hidden or removed; updates suppressed. No recreation this run.";
        if (_task is not null)
        {
            var all = AppTaskInfo.FindAll();
            if (all is null)
                throw new InvalidOperationException("FindAll returned null; cannot verify HiddenByUser. Further updates stopped.");
            var current = all.FirstOrDefault(t => t.Id == _task.Id);
            if (current is null || current.HiddenByUser)
            {
                _suppressed = true;
                return "Shell task hidden or removed; respecting user choice. Local work continues.";
            }
            _task = current;
        }
        // Shell labels are host-owned: no command lines, OSC titles or arbitrary output leave the host.
        string subtitle = $"{scenario} / {snapshot.State}";
        string safeLabel = snapshot.Label;
        AppTaskContent content = snapshot.IsTerminal
            ? AppTaskContent.CreateTextSummaryResult(safeLabel)
            : AppTaskContent.CreateSequenceOfSteps([], safeLabel);
        if (content is null)
            throw new NotSupportedException("AppTaskContent factory returned null. No task update was submitted.");
        AppTaskState state = snapshot.State switch
        {
            RunState.Completed => AppTaskState.Completed,
            RunState.Error or RunState.Unknown or RunState.Cancelled => AppTaskState.Error,
            _ => AppTaskState.Running
        };
        bool created = _task is null;
        _task ??= AppTaskInfo.Create(GroupTitle, subtitle,
            new Uri($"osctasksdemo://task/{_session:D}"),
            new Uri("ms-appx:///Assets/Square44x44Logo.scale-200.png"), content);
        if (_task is null)
            throw new NotSupportedException("AppTaskInfo.Create returned null despite IsSupported() = true. No real Shell task was created.");
        _task.UpdateTitles(GroupTitle, subtitle);
        _task.Update(state, content);
        return $"{(created ? "Create + Update" : "Update")} succeeded\n" +
            $"Id: {_task.Id}\nState read back: {_task.State}\nHiddenByUser: {_task.HiddenByUser}\n" +
            $"Subtitle: {_task.Subtitle}\nDeepLink: {_task.DeepLink}\n" +
            $"Content: {safeLabel}\n\nShell visibility must be observed separately.";
    }

    public string ClearOwnedTasks()
    {
        int count = 0;
        var all = AppTaskInfo.FindAll();
        if (all is null)
            throw new InvalidOperationException("FindAll returned null. No tasks removed; an empty collection was not verified.");
        foreach (var task in all.Where(IsOwned))
        {
            task.Remove();
            count++;
        }
        _task = null;
        _suppressed = true;
        return $"FindAll + Remove succeeded: removed {count} demo-owned task(s).";
    }

    public string InspectRoute(Uri uri)
    {
        var all = AppTaskInfo.FindAll();
        if (all is null)
            throw new InvalidOperationException("FindAll returned null; cannot inspect the activated task.");
        var task = all.FirstOrDefault(t => IsOwned(t) && t.DeepLink == uri);
        return task is null ? "Safe demo activation; task no longer exists." :
            $"Activated persisted task\nId: {task.Id}\nState: {task.State}\nSubtitle: {task.Subtitle}\nHiddenByUser: {task.HiddenByUser}";
    }
}
