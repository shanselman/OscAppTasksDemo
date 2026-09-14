Console.OutputEncoding = new System.Text.UTF8Encoding(false);
string scenario = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "success";
if (scenario is not ("success" or "failure" or "indeterminate" or "warning" or "unknown" or "crash"))
{
    Console.Error.WriteLine("Scenario must be success, failure, indeterminate, warning, unknown, or crash.");
    return 64;
}
bool fast = args.Contains("--fast");
async Task Pause() => await Task.Delay(fast ? 1 : 1600);
void Osc(string payload, bool st = false)
{
    Console.Write($"\x1b]{payload}{(st ? "\x1b\\" : "\a")}");
    Console.Out.Flush();
}

Console.WriteLine("FAKE AGENT / deterministic local simulation / no network or AI calls");
Console.WriteLine($"Scenario: {scenario}");
Osc("2;OSC demo - simulated agent", true);
Osc("133;A");
Console.Write("demo> ");
Osc("133;B");
Console.WriteLine("fake-agent (synthetic shell markers)");
await Pause();
Osc("133;C", true);
Console.WriteLine("Reading sample inputs. UTF-8 check: café.");
Osc("9;4;1;10");
await Pause();
if (scenario == "crash")
{
    Console.Error.WriteLine("Simulated abrupt process failure; no OSC finish marker.");
    return 7;
}
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
if (scenario == "failure")
{
    Osc("9;4;2;65");
    Console.WriteLine("A simulated check failed. Red progress alone does not finish the task.");
    await Pause();
    Osc("133;D;1", true);
    Osc("9;4;0;0");
    return 1;
}
Osc("9;4;1;100");
Console.WriteLine("100% reached, but final checks still running. Do not complete yet.");
await Pause();
Osc("9;4;0;0", true);
Console.WriteLine("Progress cleared; still waiting for the explicit outcome.");
await Pause();
Osc(scenario == "unknown" ? "133;D" : "133;D;0");
Console.WriteLine(scenario == "unknown" ? "Finished without reporting an outcome." : "Simulation completed successfully.");
return 0;
