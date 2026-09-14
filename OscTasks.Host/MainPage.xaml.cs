using System.ComponentModel;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OscTasks.Core;
using OscTasks.Shell;

namespace OscTasks_Host;

public sealed partial class MainPage : Page
{
    private readonly object _gate = new();
    private readonly StringBuilder _output = new();
    private readonly Queue<string> _events = new();
    private readonly ShellTaskAdapter _shell = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private TaskLifecycle _lifecycle = new();
    private TaskSnapshot? _lastPublished;
    private CancellationTokenSource? _cancellation;
    private Task? _runTask;
    private string _scenario = "success";
    private bool _dirty = true;
    private int _droppedEvents;
    private bool _trimmedOutput;
    private string? _hostError;

    public MainPage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_runTask is { IsCompleted: false }) return;
        _runTask = RunAsync();
        await _runTask;
    }

    private async Task RunAsync()
    {
        ResetView();
        _scenario = (string)Scenario.SelectedItem;
        var session = Guid.NewGuid();
        _shell.Begin(session);
        SessionLabel.Text = $"Session {session:D} / {_scenario} / UI and Shell updates throttled to 4 Hz";
        _cancellation = new CancellationTokenSource();
        SetRunning(true);
        try
        {
            string agent = Path.Combine(AppContext.BaseDirectory, "Agent", "OscTasks.Agent.exe");
            if (!File.Exists(agent)) throw new FileNotFoundException("Packaged agent missing. Run .\\Demo.ps1 to publish it before building.", agent);
            var result = await AgentProcess.RunAsync(agent, new[] { _scenario }, Receive,
                message => Receive(new(EventKind.Text, $"[stderr] {message}\n")), _cancellation.Token);
            lock (_gate)
            {
                _lifecycle.Disconnect(result.ExitCode, result.WasCancelled);
                AddEvent($"PROCESS exited {result.ExitCode}; cancelled={result.WasCancelled}");
                _dirty = true;
            }
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            lock (_gate)
            {
                _hostError = $"Process host FAILED: {ex.GetType().Name} 0x{ex.HResult:X8}: {ex.Message}";
                AddEvent(_hostError);
                _lifecycle.Disconnect(null);
                _dirty = true;
            }
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            SetRunning(false);
            Refresh();
        }
    }

    private void Receive(StreamEvent streamEvent)
    {
        lock (_gate)
        {
            _lifecycle.Accept(streamEvent);
            if (streamEvent.Kind == EventKind.Text)
            {
                _output.Append(streamEvent.Detail);
                if (_output.Length > 32768)
                {
                    _output.Remove(0, _output.Length - 32768);
                    _trimmedOutput = true;
                }
            }
            else AddEvent($"{streamEvent.Kind}: {streamEvent.Detail}");
            _dirty = true;
        }
    }

    private void AddEvent(string text)
    {
        _events.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {text}");
        while (_events.Count > 120) { _events.Dequeue(); _droppedEvents++; }
    }

    private void Refresh()
    {
        TaskSnapshot snapshot;
        lock (_gate)
        {
            if (!_dirty) return;
            snapshot = _lifecycle.Snapshot;
            TerminalOutput.Text = (_trimmedOutput ? "[older output trimmed]\n" : "") + _output;
            EventOutput.Text = (_droppedEvents > 0 ? $"[{_droppedEvents} older events trimmed]\n" : "") + string.Join('\n', _events);
            _dirty = false;
        }
        if (snapshot != _lastPublished)
        {
            _shell.Publish(snapshot, _scenario);
            _lastPublished = snapshot;
        }
        LocalState.Text = $"{snapshot.State}\n{snapshot.Label}" +
            (snapshot.Title.Length > 0 ? $"\nLocal OSC title: {snapshot.Title}" : "");
        LocalProgress.IsIndeterminate = snapshot.IsIndeterminate;
        LocalProgress.Value = snapshot.Percent ?? 0;
        ShowShellStatus();
    }

    private void ShowShellStatus()
    {
        var status = _shell.Status;
        SupportBanner.Title = _hostError is not null ? "Process host error" :
            status.IsFailure ? "Shell API failed" : status.IsSupported ? "Shell API supported" : "Unsupported / local preview only";
        SupportBanner.Message = _hostError ?? status.Message;
        SupportBanner.Severity = _hostError is not null || status.IsFailure ? InfoBarSeverity.Error :
            status.IsSupported ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        ApiOutput.Text = $"{status.Message}\n\n{_shell.LastResult}\n\nPackage identity:\n{_shell.Identity}";
        ClearButton.IsEnabled = status.IsSupported && _cancellation is null;
    }

    private void SetRunning(bool running)
    {
        RunButton.IsEnabled = !running;
        Scenario.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        ResetButton.IsEnabled = !running;
        ClearButton.IsEnabled = !running && _shell.Status.IsSupported;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();
    private void Reset_Click(object sender, RoutedEventArgs e) { ResetView(); Refresh(); }

    private void ResetView()
    {
        lock (_gate)
        {
            _output.Clear();
            _events.Clear();
            _lifecycle = new();
            _lastPublished = null;
            _droppedEvents = 0;
            _trimmedOutput = false;
            _hostError = null;
            _dirty = true;
        }
        SessionLabel.Text = "View reset. Persisted Shell tasks are unchanged; use Clear demo tasks to remove them.";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _shell.ClearOwnedTasks();
        ShowShellStatus();
    }

    public void ActivateTask(Uri uri)
    {
        SessionLabel.Text = _shell.InspectRoute(uri);
        ShowShellStatus();
    }

    public async Task StopAsync()
    {
        _cancellation?.Cancel();
        if (_runTask is not null) await _runTask;
        _timer.Stop();
    }
}
