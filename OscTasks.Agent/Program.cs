Console.OutputEncoding = new System.Text.UTF8Encoding(false);
if (args.Contains("--help"))
{
    Console.WriteLine("Usage: OscTasks.Agent [success|failure|indeterminate|warning|unknown|crash] [--fast] [--synthetic-shell-markers]");
    Console.WriteLine("Default: agent activity titles, progress and narrative; your shell owns OSC 133 lifecycle.");
    Console.WriteLine("--synthetic-shell-markers: direct-host TEST FIXTURE only. Do not combine with shell integration.");
    Console.WriteLine("The unknown scenario omits an outcome only in synthetic-marker mode; its real process exit is 0.");
    return 0;
}
if (args.Count(a => !a.StartsWith("--", StringComparison.Ordinal)) > 1 ||
    args.Any(a => a.StartsWith("--", StringComparison.Ordinal) && a is not ("--fast" or "--synthetic-shell-markers")))
{
    Console.Error.WriteLine("Invalid arguments. Use --help for supported options.");
    return 64;
}
string scenario = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "success";
if (scenario is not ("success" or "failure" or "indeterminate" or "warning" or "unknown" or "crash"))
{
    Console.Error.WriteLine("Scenario must be success, failure, indeterminate, warning, unknown, or crash.");
    return 64;
}
bool fast = args.Contains("--fast");
bool syntheticShellMarkers = args.Contains("--synthetic-shell-markers");
async Task Pause() => await Task.Delay(fast ? 1 : 1600);
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
