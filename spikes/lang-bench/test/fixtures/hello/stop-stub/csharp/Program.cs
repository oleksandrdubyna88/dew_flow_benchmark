using System.Runtime.InteropServices;

var stop = new TaskCompletionSource();
using PosixSignalRegistration registration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    stop.TrySetResult();
});
Console.Out.Write("ready\n");
Console.Out.Flush();
await stop.Task;
Console.Out.Write("TERM\n");
Console.Out.Flush();
