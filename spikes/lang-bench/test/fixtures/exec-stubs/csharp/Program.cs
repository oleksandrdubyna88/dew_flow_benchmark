using System.Net;
using System.Net.Sockets;
using System.Text;

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
File.WriteAllText("ready.port.tmp", ((IPEndPoint)listener.LocalEndpoint).Port.ToString());
File.Move("ready.port.tmp", "ready.port", true);
while (true)
{
    using var client = listener.AcceptTcpClient();
    using var reader = new StreamReader(client.GetStream(), Encoding.ASCII);
    var line = reader.ReadLine() ?? "";
    client.GetStream().Write(Encoding.ASCII.GetBytes(line.Trim().ToUpperInvariant() + "\n"));
}
