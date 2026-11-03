// The execution probe's .NET program (E1.S8b): a toy, never a task of the series. One argument, the mode (see MODES in programs.mjs).
using System.Globalization;

const int Mib = 1024 * 1024;
var mode = args.Length > 0 ? args[0] : "report";
switch (mode)
{
    case "report":
        var info = GC.GetGCMemoryInfo();
        Console.WriteLine($"{{\"totalAvailableBytes\":{info.TotalAvailableMemoryBytes},\"heapHardLimit\":\"{AppContext.GetData("GCHeapHardLimit")}\",\"cgroupVisible\":{(Directory.Exists("/sys/fs/cgroup") ? "true" : "false")},\"cgroupSelf\":\"{File.ReadAllText("/proc/self/cgroup").Trim()}\",\"cgroupChildren\":{(Directory.Exists("/sys/fs/cgroup") ? Directory.GetDirectories("/sys/fs/cgroup").Length : -1)}}}");
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
    case "medium-buffers": // live byte arrays = 45% of the limit (args[1], MiB), heldData to the end, plus 2 GiB of short-lived byte arrays
    {
        var target = long.Parse(args[1]) * Mib * 45 / 100;
        var heldData = new List<byte[]>();
        for (long held = 0; held < target; held += Mib)
        {
            var block = new byte[Mib];
            Array.Fill(block, (byte)1);
            heldData.Add(block);
        }
        for (var i = 0; i < 8192; i++)
        {
            var block = new byte[256 * 1024];
            Array.Fill(block, (byte)i);
        }
        GC.KeepAlive(heldData);
        Console.WriteLine("OK");
        break;
    }
    case "near-limit": // live byte arrays = 80% of the limit (args[1], MiB), kept to the end, plus 1 GiB of short-lived byte arrays
    {
        var nearTarget = long.Parse(args[1]) * Mib * 80 / 100;
        var nearData = new List<byte[]>();
        for (long held = 0; held < nearTarget; held += Mib)
        {
            var block = new byte[Mib];
            Array.Fill(block, (byte)1);
            nearData.Add(block);
        }
        for (var i = 0; i < 4096; i++)
        {
            var block = new byte[256 * 1024];
            Array.Fill(block, (byte)i);
        }
        GC.KeepAlive(nearData);
        Console.WriteLine("OK");
        break;
    }
    case "objects-70": // live ordinary objects = 70% of the limit by the heap after a full GC, kept to the end, plus 1 GiB of short-lived small objects
    {
        var objTarget = long.Parse(args[1]) * Mib * 70 / 100;
        var objData = new List<Rec>();
        while (GC.GetTotalMemory(true) < objTarget)
            for (var i = 0; i < 20000; i++) objData.Add(new Rec { A = i, B = i, C = i });
        Console.Error.WriteLine($"live-heap-after-full-gc-MiB={GC.GetTotalMemory(true) / Mib}");
        Rec? objSink = null;
        for (long i = 0; i < 11_000_000; i++) objSink = new Rec { A = i };
        GC.KeepAlive(objData);
        GC.KeepAlive(objSink);
        Console.WriteLine("OK");
        break;
    }
    case "mixed": // an object graph at 45% of the limit plus byte arrays at 35% (both kept to the end), plus 1 GiB of short-lived allocations of both kinds
    {
        var mixLimit = long.Parse(args[1]) * Mib;
        var mixObjects = new List<Rec>();
        while (GC.GetTotalMemory(true) < mixLimit * 45 / 100)
            for (var i = 0; i < 20000; i++) mixObjects.Add(new Rec { A = i, B = i, C = i });
        var mixBytes = new List<byte[]>();
        for (long held = 0; held < mixLimit * 35 / 100; held += Mib)
        {
            var block = new byte[Mib];
            Array.Fill(block, (byte)1);
            mixBytes.Add(block);
        }
        Console.Error.WriteLine($"live-heap-after-full-gc-MiB={GC.GetTotalMemory(true) / Mib}");
        Rec? mixSink = null;
        for (long i = 0; i < 5_500_000; i++) mixSink = new Rec { A = i };
        for (var i = 0; i < 2048; i++)
        {
            var block = new byte[256 * 1024];
            Array.Fill(block, (byte)i);
        }
        GC.KeepAlive(mixObjects);
        GC.KeepAlive(mixBytes);
        GC.KeepAlive(mixSink);
        Console.WriteLine("OK");
        break;
    }
    case "medium-objects": // live ordinary objects (records with numbers and a short array) = 40% of the limit by the heap after a full GC, heldData to the end, plus 2 GiB of short-lived small objects
    {
        var target = long.Parse(args[1]) * Mib * 40 / 100;
        var heldData = new List<Rec>();
        while (GC.GetTotalMemory(true) < target)
            for (var i = 0; i < 20000; i++) heldData.Add(new Rec { A = i, B = i, C = i });
        Console.Error.WriteLine($"live-heap-after-full-gc-MiB={GC.GetTotalMemory(true) / Mib}");
        Rec? sink = null;
        for (long i = 0; i < 22_000_000; i++) sink = new Rec { A = i };
        GC.KeepAlive(heldData);
        GC.KeepAlive(sink);
        Console.WriteLine("OK");
        break;
    }
    case "overlimit-objects": // live ordinary objects that only grow, to about 4 GiB: no limit of 256 or 512 MiB can hold them
    {
        var grown = new List<Rec>();
        for (long i = 0; i < 40_000_000; i++) grown.Add(new Rec { A = i });
        GC.KeepAlive(grown);
        Console.WriteLine("OK");
        break;
    }
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

sealed class Rec
{
    public double A, B, C;
    public int[] Arr = new int[4];
}
