using System.Globalization;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
if (args.Contains("--help"))
{
    Console.WriteLine("Usage: OscTasks.Agent [success|failure|indeterminate|warning|unknown|crash|title-only|conversation] [--delay-ms 0..10000 | --fast] [--synthetic-shell-markers]");
    Console.WriteLine("Default: agent activity titles, progress and narrative; your shell owns OSC 133 lifecycle.");
    Console.WriteLine("--synthetic-shell-markers: direct-host TEST FIXTURE only. Do not combine with shell integration.");
    Console.WriteLine("The unknown scenario omits an outcome only in synthetic-marker mode; its real process exit is 0.");
    Console.WriteLine("title-only/conversation emit no progress. conversation waits after its answer, then exits; no turn-completion signal.");
    return 0;
}
string? scenarioArgument = null;
int delayMs = 1600;
bool fast = false;
bool hasDelay = false;
bool syntheticShellMarkers = false;
for (int i = 0; i < args.Length; i++)
{
    string argument = args[i];
    if (argument == "--fast" && !fast && !hasDelay) { fast = true; delayMs = 1; }
    else if (argument == "--synthetic-shell-markers" && !syntheticShellMarkers) syntheticShellMarkers = true;
    else if (argument == "--delay-ms" && !hasDelay && !fast && i + 1 < args.Length &&
        int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out int parsedDelay) &&
        parsedDelay is >= 0 and <= 10000)
    {
        hasDelay = true;
        delayMs = parsedDelay;
    }
    else if (!argument.StartsWith("--", StringComparison.Ordinal) && scenarioArgument is null)
        scenarioArgument = argument;
    else
    {
        Console.Error.WriteLine("Invalid or conflicting arguments. Use --help for supported options.");
        return 64;
    }
}
string scenario = scenarioArgument ?? "success";
if (scenario is not ("success" or "failure" or "indeterminate" or "warning" or "unknown" or "crash" or "title-only" or "conversation"))
{
    Console.Error.WriteLine("Unknown scenario. Use --help for supported scenarios.");
    return 64;
}
async Task Pause() => await Task.Delay(delayMs);
void Osc(string payload, bool st = false)
{
    Console.Write($"\x1b]{payload}{(st ? "\x1b\\" : "\a")}");
    Console.Out.Flush();
}
void ShellMarker(string payload, bool st = false)
{
    if (syntheticShellMarkers) Osc(payload, st);
}

Console.WriteLine("FAKE AGENT / deterministic local simulation / no network or AI calls");
Console.WriteLine($"Scenario: {scenario}");
Console.WriteLine(syntheticShellMarkers
    ? "TEST FIXTURE: synthetic shell lifecycle enabled for the direct process-output host."
    : "Agent mode: the surrounding shell owns command lifecycle.");
if (syntheticShellMarkers)
{
    ShellMarker("133;A");
    Console.Write("demo> ");
    ShellMarker("133;B");
    Console.WriteLine("fake-agent (synthetic shell markers)");
}
await Pause();
ShellMarker("133;C", true);
if (scenario is "title-only" or "conversation")
{
    Console.WriteLine("Generic session fixture: titles are display metadata, not steps, progress or agent turns.");
    Osc("2;Sample session");
    await Pause();
    Osc("2;Sample response");
    Osc("2;Sample response", true);
    Console.WriteLine("Simulated answer: the sample is ready. No files were changed.");
    await Pause();
    Osc("2;Sample session available", true);
    Console.WriteLine("The foreground process is still open. Session active does not mean working or waiting for required input.");
    if (scenario == "conversation")
        await Task.Delay(fast || delayMs == 0 ? delayMs : Math.Max(2000, delayMs * 3));
    else await Pause();
    Console.WriteLine("Bounded session interval finished; exiting normally now.");
    ShellMarker("133;D;0", true);
    return 0;
}
Osc("2;Inspecting project");
Console.WriteLine("Reading sample inputs. UTF-8 check: café.");
Osc("9;4;1;10");
await Pause();
if (scenario == "crash")
{
    Console.Error.WriteLine("Simulated abrupt process failure (exit 7).");
    return 7;
}
Osc("2;Editing files", true);
Console.WriteLine("Activity titles follow the demo's opt-in sequential-step convention.");
if (scenario == "indeterminate")
{
    Osc("9;4;3;0", true);
    Console.WriteLine("Thinking... duration unknown. The host remains responsive.");
    await Pause();
    await Pause();
}
Osc(scenario == "warning" ? "9;4;4;45" : "9;4;1;45", true);
Console.WriteLine(scenario == "warning"
    ? "Warning-colored progress only: not a request for user input."
    : "Preparing a simulated change.");
await Pause();
Osc("2;Running tests");
Osc("2;Running tests", true);
Osc("9;4;1;65");
Console.WriteLine("Running simulated tests. Repeating a title does not add another step.");
await Pause();
if (scenario == "failure")
{
    Osc("9;4;2;65");
    Console.WriteLine("A simulated check failed. Red progress alone does not finish the task.");
    await Pause();
    ShellMarker("133;D;1", true);
    Osc("9;4;0;0");
    return 1;
}
Osc("9;4;1;100");
Console.WriteLine("100% reached, but final checks still running. Do not complete yet.");
await Pause();
Osc("9;4;0;0", true);
Console.WriteLine("Progress cleared; still waiting for the explicit outcome.");
await Pause();
ShellMarker(scenario == "unknown" ? "133;D" : "133;D;0");
Console.WriteLine(scenario == "unknown" && syntheticShellMarkers
    ? "Finished without reporting an outcome."
    : "Simulation completed successfully (process exit 0).");
return 0;
