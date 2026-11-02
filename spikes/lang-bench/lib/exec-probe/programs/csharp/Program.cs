// The execution probe's .NET program (E1.S8b): a toy, never a task of the series. One argument, the mode: report, culture, churn, overlimit.
using System.Globalization;

const int Mib = 1024 * 1024;
var mode = args.Length > 0 ? args[0] : "report";
switch (mode)
{
    case "report":
        var info = GC.GetGCMemoryInfo();
        Console.WriteLine($"{{\"totalAvailableBytes\":{info.TotalAvailableMemoryBytes},\"heapHardLimit\":\"{AppContext.GetData("GCHeapHardLimit")}\",\"cgroupVisible\":{(Directory.Exists("/sys/fs/cgroup") ? "true" : "false")}}}");
        break;
    case "culture":
        Console.WriteLine(CultureInfo.CurrentCulture.Name);
        break;
    case "churn": // bounded live data (32 MiB), 4 GiB allocated over the run: far more than the limit, almost all of it garbage at once
        var live = new byte[32 * Mib];
        Array.Fill(live, (byte)1);
        for (var i = 0; i < 4096; i++)
        {
            var block = new byte[Mib];
            Array.Fill(block, (byte)i);
            live[i % live.Length] = block[0];
        }
        Console.WriteLine("OK");
        break;
    case "overlimit": // live data that only grows, to 3 GiB: no limit of 256 or 512 MiB can hold it
        var kept = new List<byte[]>();
        for (var i = 0; i < 48; i++)
        {
            var block = new byte[64 * Mib];
            Array.Fill(block, (byte)1);
            kept.Add(block);
        }
        Console.WriteLine("OK");
        break;
}
