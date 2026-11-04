using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateApplicationBuilder(args).Build();
CancellationToken stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
var listener = new TcpListener(IPAddress.Loopback, int.Parse(Environment.GetEnvironmentVariable("PORT")!));
listener.Start();
await host.StartAsync();
try
{
    while (true)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(stopping);
        using NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        string? line = await reader.ReadLineAsync(stopping);
        if (line is not null)
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"hello {line}\n"), stopping);
        }
    }
}
catch (OperationCanceledException)
{
    // the stop
}
listener.Stop();
await host.StopAsync();
