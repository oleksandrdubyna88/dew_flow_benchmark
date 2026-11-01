using System.Collections;
using System.Diagnostics;
using Bench.Cli;
using ModelArms;
using ModelArms.Adapters;
using ModelArms.Verbs;

// lock-crasher <experiment-root> <run>
// Runs `lock <run>` in this process and kills the process (SIGKILL) right after the second mode has been applied.
// Never returns normally; if the lock finishes without the kill, it exits 9 so the test sees that nothing was proved.
var environment = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
{
    environment[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
}

var input = new VerbInput("lock", [args[1]], CommandLine.Parse(["lock", "--experiment", args[0]]), Console.Out, Console.Error, environment, ModuleFiles.Beside(AppContext.BaseDirectory), CancellationToken.None);
var seams = IsolationSeams.Real with { Hands = new IsolationHands(null, null, (index, _) => KillAfterTheSecond(index), null) };
await ExperimentGate.RunAsync(input, admitted => LockVerb.RunAsync(admitted, seams));
return 9;

static void KillAfterTheSecond(int index)
{
    if (index == 1)
    {
        Process.GetCurrentProcess().Kill();
    }
}
