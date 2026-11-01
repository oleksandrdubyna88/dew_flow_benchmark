using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The kit's recording shim (moved from <c>ArmGitWideningTests</c>, E4.S0): a git that writes one block per call to a marker file and then runs the host's git — <c>CALL &lt;argv&gt;</c>, <c>CWD &lt;pwd&gt;</c> (the folder the launcher started it in: what every "from the experiment root" assertion reads), then <c>ENV name=value</c> for every
/// <c>GIT_*</c> and for the names the tests care about (<c>PGPASSWORD</c> among them: an assertion that a name is ABSENT is green over any code unless the shim would have recorded it). E4.S2 moved the plant helpers here (<see cref="PlantProgram"/>, <see cref="Ran"/>, <see cref="ProveLiveAsync"/>, <see cref="HostGitAsync"/>): a program a config or a hook can name, the sentinel it writes, and the proof that the host's own git runs it.
/// <para>
/// What this does NOT prove: Windows (the shim is a shell script, and <c>$PWD</c> is the shell's logical path, which equals the real one for the scratch folders under <c>/tmp</c> this suite uses).
/// </para></summary>
internal static class Sentinel
{
    private static int _dirtied;

    /// <summary>The file a planted program appends a line to.</summary>
    public static string Marker(TempFolder temp) => temp.Combine("sentinel.log");

    public static bool Ran(TempFolder temp) => System.IO.File.Exists(Marker(temp));

    /// <summary>A program a clone's config or hooks folder can name: it appends a line to the sentinel and, as a filter must, passes its input through (<paramref name="body"/> <c>cat</c>). An editor, a hook, a signer or a <c>!cmd</c> takes the body <c>exit 0</c> — a <c>cat</c> would wait on an inherited stdin — and a signer <c>exit 1</c>.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public static string PlantProgram(TempFolder temp, string name, string? body = null) =>
        ShimFiles.Write(temp.Combine("plant", name), $"#!/bin/sh\necho \"$0 ran\" >> '{Marker(temp)}'\n{body ?? "cat"}\n");

    /// <summary>Makes <paramref name="file"/> stat-dirty without changing a byte: a distinct, older modification time every call — a repeated one would let a later "no program ran" prove nothing, because git would find the index already refreshed.</summary>
    public static void Redirty(string file) => System.IO.File.SetLastWriteTimeUtc(file, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(Interlocked.Increment(ref _dirtied)));

    /// <summary>The host's git, EXIT CODE IGNORED, under <paramref name="environment"/>: for proving a plant live (a plant that makes git fail still ran its program).</summary>
    public static async Task HostGitAsync(string folder, IReadOnlyDictionary<string, string> environment, CancellationToken ct, params string[] arguments)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, arguments, folder, TimeSpan.FromSeconds(60), string.Empty, environment, ct);
        attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>(attempt.Describe);
    }

    /// <summary>The plant is LIVE: the host's own git, asked the same kind of question over a stat-dirty file (when <paramref name="dirtyFile"/> is given), ran the program. Then the sentinel is deleted and the file made dirty again, so the question under test starts from the same state.</summary>
    public static async Task ProveLiveAsync(TempFolder temp, IReadOnlyDictionary<string, string> environment, CancellationToken ct, string folder, string? dirtyFile, params string[] hostQuestion)
    {
        if (dirtyFile is not null)
        {
            Redirty(dirtyFile);
        }

        await HostGitAsync(folder, environment, ct, hostQuestion);
        Ran(temp).Should().BeTrue("the plant must be proven live with the host's git first, or 'no program ran' proves nothing");
        System.IO.File.Delete(Marker(temp));
        if (dirtyFile is not null)
        {
            Redirty(dirtyFile);
        }
    }

    /// <summary>The names, besides <c>GIT_*</c>, whose presence a block records.</summary>
    private const string RecordedNames = "GIT_*|GITHUB_TOKEN=*|CLAUDE_CODE_OAUTH_TOKEN=*|BENCH_DB=*|SSH_AUTH_SOCK=*|KEEP_ME=*|PGPASSWORD=*";

    /// <summary>The shim's script body (after the shebang): the recording, then — when <paramref name="printed"/> is given — an answer to <c>--version</c> (<c>git version &lt;printed&gt;</c>, exit 0, recorded like every call), then the host's git.</summary>
    public static string Body(string marker, string? printed = null) =>
        $"echo \"CALL $*\" >> '{marker}'\necho \"CWD $PWD\" >> '{marker}'\n"
        + $"/usr/bin/env | while IFS= read -r l; do case \"$l\" in {RecordedNames}) echo \"ENV ${{l%%=*}}=${{l#*=}}\" >> '{marker}';; esac; done\n"
        + (printed is null ? string.Empty : $"for a; do :; done\nif [ \"$a\" = --version ]; then echo 'git version {printed}'; exit 0; fi\n")
        + $"exec '{ExperimentRoot.RealGit}' \"$@\"\n";

    /// <summary>A shim body that answers <c>--version</c> with <c>git version &lt;printed&gt;</c> WITHOUT recording it, and appends <c>$*</c> of every other call to <paramref name="marker"/> before the host's git: a marker that is absent proves no repository question was asked (the version question is the only one it lets through unlogged).</summary>
    public static string LogsAllButVersion(string marker, string printed) =>
        $"for a; do :; done\nif [ \"$a\" = --version ]; then echo 'git version {printed}'; exit 0; fi\necho \"$*\" >> '{marker}'\nexec '{ExperimentRoot.RealGit}' \"$@\"\n";

    /// <summary>The recording shim written at <c>&lt;temp&gt;/bin/recording-git</c>: one block per call appended to <paramref name="marker"/>, then the host's git.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public static string RecordingShim(TempFolder temp, string marker) =>
        ShimFiles.Write(temp.Combine("bin", "recording-git"), "#!/bin/sh\n" + Body(marker));

    /// <summary>The blocks <paramref name="marker"/> holds, in order: the argv, the folder the call started in and the recorded environment lines.</summary>
    public static IReadOnlyList<(string Call, string Cwd, IReadOnlyList<string> Env)> Blocks(string marker)
    {
        var blocks = new List<(string Call, string Cwd, List<string> Env)>();
        foreach (var line in File.ReadAllLines(marker))
        {
            if (line.StartsWith("CALL ", StringComparison.Ordinal))
            {
                blocks.Add((line[5..], string.Empty, []));
            }
            else if (line.StartsWith("CWD ", StringComparison.Ordinal))
            {
                blocks[^1] = blocks[^1] with { Cwd = line[4..] };
            }
            else if (line.StartsWith("ENV ", StringComparison.Ordinal))
            {
                blocks[^1].Env.Add(line[4..]);
            }
        }

        return [.. blocks.Select(block => (block.Call, block.Cwd, (IReadOnlyList<string>)block.Env))];
    }
}
