using System.Diagnostics;
using ModelArms.Adapters;

// gate-holder <lock-path> <child-executable> [child arguments...]
// Prints "held <own pid>" once it holds the lock and "child <pid>" once the child runs, then waits forever.
var gate = new FileGateLock(TimeProvider.System, TimeSpan.FromMilliseconds(100));
using var held = gate.TryAcquire(args[0]);
if (held is null)
{
    Console.Error.WriteLine("gate-holder: the lock is taken");
    return 3;
}

var start = new ProcessStartInfo(args[1]) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (var argument in args.Skip(2))
{
    start.ArgumentList.Add(argument);
}

using var child = Process.Start(start)!;
Console.WriteLine($"held {Environment.ProcessId}");
Console.WriteLine($"child {child.Id}");
Console.Out.Flush();
Thread.Sleep(Timeout.Infinite);
return 0;
