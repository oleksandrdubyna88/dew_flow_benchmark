using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one run of <c>make</c> left on the two writers.</summary>
internal sealed record MakeRun(int Code, string Output, string Error)
{
    public string Everything => Output + "\n" + Error;
}

/// <summary>The scratch target of E3.S3's tests: a <see cref="TargetBench"/> whose <c>prep-base</c> has RUN (so <c>base/</c> is the built git checkout <c>make</c> copies and <c>baseline.json</c> is there), the shims' calls cleared afterwards, the experiment's runs
/// replaced by one run naming the arms a test asks for, and the coai settings file holding bytes a test can tell (a non-ASCII letter and a CRLF, so a copy through text would not be byte-equal). Everything is under the experiment root's own <see cref="TempFolder"/>.</summary>
internal sealed class MakeKit : IDisposable
{
    /// <summary>The bytes of the operator's settings file: a copy through UTF-16 text, a trimmed newline or a re-encoded BOM would each change them.</summary>
    public static readonly byte[] SettingsBytes = Encoding.UTF8.GetBytes("{\"vault\":\"s-é\",\"order\":[3,1,2]}\r\n");

    private byte _token = 0x30;

    private MakeKit(TargetBench bench) => Bench = bench;

    public TargetBench Bench { get; }

    public ExperimentRoot Root => Bench.Root;

    public RootPaths Layout => new(Root.Root);

    public string SettingsFile => Root.ExperimentOf().Coai.Settings;

    /// <summary>The target's branch every arm commits to.</summary>
    public string Branch => Root.ExperimentOf().Target.Branch;

    public static async Task<MakeKit> CreateAsync(params string[] arms)
    {
        var bench = await TargetBench.CreateAsync();
        try
        {
            var (code, output, error) = await bench.PrepBaseAsync();
            code.Should().Be(0, "the fixture's base is built by prep-base itself: " + error + output);
            File.Delete(bench.Calls);
            var rounds = string.Join(',', arms.Select(arm => $"\"{arm}\""));
            bench.Root.Edit(top => top["runs"] = JsonNode.Parse("{\"part6\":{\"rounds\":[[" + rounds + "]]}}"));
            File.WriteAllBytes(bench.Root.ExperimentOf().Coai.Settings, SettingsBytes);
            return new MakeKit(bench);
        }
        catch
        {
            bench.Dispose();
            throw;
        }
    }

    /// <summary><c>make</c> through the console's entry point, with the production clock and walls.</summary>
    public async Task<MakeRun> MakeAsync(CancellationToken? stopping = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ModelArmsApp.Program.RunAsync(["make", "--experiment", Root.Root], output, error, Root.Environment, stopping ?? TestContext.Current.CancellationToken);
        return new MakeRun(code, output.ToString(), error.ToString());
    }

    /// <summary><c>status</c> through the same entry point.</summary>
    public async Task<MakeRun> StatusAsync()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ModelArmsApp.Program.RunAsync(["status", "--experiment", Root.Root], output, error, Root.Environment, TestContext.Current.CancellationToken);
        return new MakeRun(code, output.ToString(), error.ToString());
    }

    /// <summary>Writes the arm map with these arms, each given a token of a counter's byte (two arms never share one), and returns it. Mode 0600, as <c>make</c> leaves it.</summary>
    public ArmMap WriteMap(params string[] arms)
    {
        var map = ArmMap.Extend(ReadMap(), arms, () => [0, 0, 0, 0, 0, 0, 0, ++_token]);
        Root.Write("arms.map.json", map.ToJson());
        return map;
    }

    public ArmMap ReadMap() =>
        File.Exists(Layout.ArmMapFile) ? ArmMap.Read(File.ReadAllText(Layout.ArmMapFile)).Ok() : ArmMap.Empty;

    public ArmPaths Paths(string arm) => Layout.Arm(ReadMap().TokenOf(arm).Ok());

    /// <summary>Every token of the map: what no output line may hold.</summary>
    public IReadOnlyList<string> Tokens => [.. ReadMap().Entries.Select(entry => entry.Value)];

    /// <summary>A file with the given text at the full path, its folders made.</summary>
    public static string Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>A timing line the module's own writer spells, for an arm that RAN (<paramref name="start"/>) or one that only stopped.</summary>
    public static string TimingText(string arm, bool start)
    {
        var at = new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);
        return TimingLines.Write(start
            ? new TimingLine.Start(arm, "1-code", 1, "fresh", "s", "m", at, 600, "23:12 UTC (00:12 +01:00)", "abc1234", "x.a1.jsonl")
            : new TimingLine.Stopped(arm, "1-code", 5, "the operator stopped it", at));
    }

    /// <summary>A half-made arm: its folder, a marker-less leftover in <c>arm/</c>, and no timing — what a <c>make</c> that was killed leaves.</summary>
    public string Leftover(string arm, string relative = "half.txt")
    {
        var path = Path.Combine(Paths(arm).CloneFolder, relative);
        return Put(path, "left by a make that was killed\n");
    }

    /// <summary>Every entry under <paramref name="folder"/> as one text line each — its relative path, its kind, its mode, a link's text or a file's hash — sorted; a link is never entered. Equal text before and after is "unchanged".</summary>
    public static IReadOnlyList<string> Describe(string folder, Func<string, bool>? skip = null) =>
        [.. Walk(folder, folder).Where(line => skip is null || !skip(line.Split('|')[0])).OrderBy(line => line.Split('|')[0], StringComparer.Ordinal)];

    private static IEnumerable<string> Walk(string root, string folder)
    {
        foreach (var info in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            var relative = Path.GetRelativePath(root, info.FullName).Replace('\\', '/');
            if (info.LinkTarget is { } target)
            {
                yield return $"{relative}|link|{target}";
            }
            else if (info is DirectoryInfo directory)
            {
                yield return $"{relative}|dir|{directory.UnixFileMode}";
                foreach (var inner in Walk(root, info.FullName))
                {
                    yield return inner;
                }
            }
            else
            {
                yield return $"{relative}|file|{info.UnixFileMode}|{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(info.FullName)))}";
            }
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    /// <summary>A FIFO at <paramref name="path"/> (its folder made); the fixture fails when the host cannot make one.</summary>
    public static string Fifo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        mkfifo(path, Convert.ToUInt32("600", 8)).Should().Be(0, "the fixture makes a FIFO");
        return path;
    }

    public static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "modes, links and FIFOs are POSIX");

    public void Dispose() => Bench.Dispose();
}
