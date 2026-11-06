// The rehearsal's daemon (quick run): answers "pong\n" to any bytes that hold "ping", exits 0 on SIGTERM. Not a solution of any task.
// HELLO_MODE (set by a rehearsal scenario's env line) makes it fail on purpose: wrong (a wrong reply), sigdie (SIGTERM kills it), exit3 (exits 3 on SIGTERM), hog (holds 700 MiB).
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

var mode = Environment.GetEnvironmentVariable("HELLO_MODE") ?? "";
var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");
var reply = Encoding.ASCII.GetBytes(mode == "wrong" ? "pang\n" : "pong\n");
byte[]? hog = null;
if (mode == "hog")
{
    hog = new byte[700 * 1024 * 1024];
    for (var i = 0; i < hog.Length; i += 4096) hog[i] = 1;
}
var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();
using var done = new ManualResetEventSlim();
PosixSignalRegistration? registration = mode == "sigdie" ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; done.Set(); });
_ = Task.Run(async () =>
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(async () =>
        {
            using var c = client;
            var stream = c.GetStream();
            var buffer = new byte[256];
            int n;
            while ((n = await stream.ReadAsync(buffer)) > 0)
            {
                if (Encoding.ASCII.GetString(buffer, 0, n).Contains("ping", StringComparison.Ordinal)) await stream.WriteAsync(reply);
            }
        });
    }
});
done.Wait();
GC.KeepAlive(hog);
GC.KeepAlive(registration);
return mode == "exit3" ? 3 : 0;
