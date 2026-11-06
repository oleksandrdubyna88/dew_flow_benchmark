using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;

var listener = new TcpListener(IPAddress.Loopback, int.Parse(Environment.GetEnvironmentVariable("PORT")!));
listener.Start();
var done = new ManualResetEventSlim();
PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; done.Set(); });
_ = Task.Run(async () =>
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(async () =>
        {
            var stream = client.GetStream();
            var buf = new byte[65536];
            int n;
            while ((n = await stream.ReadAsync(buf)) > 0)
            {
                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("echo:"));
                await stream.WriteAsync(buf.AsMemory(0, n));
            }
        });
    }
});
done.Wait();
return 0;
