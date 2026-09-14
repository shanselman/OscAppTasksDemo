using System.Diagnostics;

namespace OscTasks.Core;

public sealed record ProcessResult(int ExitCode, bool WasCancelled);

public static class AgentProcess
{
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        Action<StreamEvent> receive, Action<string> stderr, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        var parser = new OscParser(receive);
        Task output = ReadOutputAsync(process.StandardOutput.BaseStream, parser);
        Task error = ReadErrorAsync(process.StandardError, stderr);
        bool cancelled = false;
        try
        {
            var pending = new List<Task> { output, error, process.WaitForExitAsync(cancellationToken) };
            while (pending.Count > 0)
            {
                Task completed = await Task.WhenAny(pending);
                await completed;
                pending.Remove(completed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            KillIfRunning(process);
            await process.WaitForExitAsync();
        }
        finally
        {
            // Always reap our child, including when stream handling or waiting fails.
            KillIfRunning(process);
            await process.WaitForExitAsync();
            await Task.WhenAll(output, error);
        }
        return new(process.ExitCode, cancelled);
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The child exited between the check and Kill.
        }
    }

    private static async Task ReadOutputAsync(Stream stream, OscParser parser)
    {
        byte[] buffer = new byte[2048];
        int count;
        while ((count = await stream.ReadAsync(buffer)) != 0) parser.Feed(buffer.AsSpan(0, count));
        parser.Complete();
    }

    private static async Task ReadErrorAsync(StreamReader reader, Action<string> receive)
    {
        char[] buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
            receive(OscParser.SafeText(new string(buffer, 0, count), 1024));
    }
}
