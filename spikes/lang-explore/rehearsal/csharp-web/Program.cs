// The rehearsal's web server (quick run): GET /hello answers "hello", exits 0 on SIGTERM. Not a solution of any task.
// HELLO_MODE makes it fail on purpose: wrong (a wrong body), sigdie (SIGTERM kills it), exit3 (exits 3 on SIGTERM), hog (holds 700 MiB).
using System.Runtime.InteropServices;

var mode = Environment.GetEnvironmentVariable("HELLO_MODE") ?? "";
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
byte[]? hog = null;
if (mode == "hog")
{
    hog = new byte[700 * 1024 * 1024];
    for (var i = 0; i < hog.Length; i += 4096) hog[i] = 1;
}
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Logging.ClearProviders();
var app = builder.Build();
app.MapGet("/hello", () => Results.Text(mode == "wrong" ? "hullo" : "hello", "text/plain"));
if (mode == "sigdie")
{
    using var kill = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = false; Environment.FailFast("sigdie"); });
    app.Run();
    return 0;
}
app.Run();
GC.KeepAlive(hog);
return mode == "exit3" ? 3 : 0;
