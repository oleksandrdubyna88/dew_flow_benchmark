using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S3 — two additions to the fake CLI (<c>tests/FakeCli</c>) the step runner's tests are built on: a replay chosen by the
/// CALL number (<c>stdoutByCall</c>), so one test scripts a step's attempts in order; and the empty transcript file claude leaves for a session
/// (<c>transcripts: true</c>), so a re-entry through <c>run-arm</c> finds one and resumes. Every launch goes through the shared launcher
/// to the fake; nothing paid is called.
/// <para>
/// What this does NOT prove: that the transcript path is the one <c>SessionTranscripts.PathOf</c> spells — that type is E2.S2's and does not exist yet, so the
/// path here is written out a second time (the slug rule, on purpose: the fake references nothing of the module); once it lands, the integration adds the
/// comparison. Nor does it prove where a real claude puts its transcript on a host this was not measured on.
/// </para></summary>
public sealed class FakeCliByCallTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] Argv(FakeCli fake, params string[] extra) => ["-p", "--model", fake.Model, .. extra];

    private static async Task<string> LaunchAsync(FakeCli fake, string cwd, IReadOnlyDictionary<string, string>? environment = null, params string[] extra)
    {
        var attempt = await ProcessRunner.RunAsync(FakeCli.Executable, Argv(fake, extra), cwd, TimeSpan.FromMinutes(1), "prompt", environment, Ct);
        var result = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        result.ExitCode.Should().Be(0, result.Output);
        return result.StandardOutput;
    }

    [Fact]
    public async Task Each_call_replays_its_own_file_and_an_unlisted_call_falls_back()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var (a, c, f) = (temp.File("stream-a\n", "a.ndjson"), temp.File("stream-c\n", "c.ndjson"), temp.File("stream-f\n", "f.ndjson"));
        fake.Rewrite(new JsonObject
        {
            ["stdoutByCall"] = new JsonObject { ["1"] = a, ["3"] = c },
            ["stdoutFile"] = f,
        });
        var stdouts = new List<string>();

        for (var call = 1; call <= 4; call++)
        {
            stdouts.Add(await LaunchAsync(fake, temp.Path));
        }

        stdouts.Should().Equal("stream-a", "stream-f", "stream-c", "stream-f");
        fake.Calls().Select(call => call.Call).Should().Equal(1, 2, 3, 4);
    }

    /// <summary>The slug rule written out a second time — every character outside <c>[A-Za-z0-9]</c> becomes <c>-</c> — so the test does not borrow the fake's
    /// own spelling of the path it is checking.</summary>
    private static string Slug(string cwd) => Regex.Replace(cwd, "[^A-Za-z0-9]", "-");

    /// <summary>The cwd the fake logged for its call: what the process itself reports (a symbolic link in the temp folder's path would differ from the
    /// path the launcher was handed, and claude slugs the one it sees).</summary>
    private static string SeenCwd(FakeCli fake) => fake.Calls()[^1].Cwd;

    private static string Transcript(string home, string cwd, Guid session) => Path.Combine(home, "projects", Slug(cwd), $"{session:D}.jsonl");

    private static IReadOnlyDictionary<string, string> Environment(FakeCli fake, string home, string? configDir = null)
    {
        var environment = new Dictionary<string, string>
        {
            ["HOME"] = home,
            ["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            ["FAKE_CLI_HOME"] = fake.Home,
        };

        foreach (var name in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64" })
        {
            if (System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
            {
                environment[name] = value;
            }
        }

        if (configDir is not null)
        {
            environment["CLAUDE_CONFIG_DIR"] = configDir;
        }

        return environment;
    }

    [Fact]
    public async Task A_launch_with_a_session_id_leaves_the_transcript_claude_would_when_asked()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var cwd = temp.Folder("r", "abc", "arm");
        var home = temp.Folder("home");
        var stream = temp.File("stream\n", "f.ndjson");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = stream });

        var fresh = Guid.NewGuid();
        await LaunchAsync(fake, cwd, Environment(fake, home), "--session-id", fresh.ToString("D"));
        var freshFile = Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), fresh);
        File.Exists(freshFile).Should().BeTrue($"claude leaves {freshFile} for a session it was given with --session-id");
        new FileInfo(freshFile).Length.Should().Be(0, "the file is empty: the fake has no conversation to record");

        var resumed = Guid.NewGuid();
        await LaunchAsync(fake, cwd, Environment(fake, home), "--resume", resumed.ToString("D"));
        File.Exists(Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), resumed)).Should().BeTrue("a resumed session has its transcript too");

        var config = temp.Combine("cfg");
        var relocated = Guid.NewGuid();
        await LaunchAsync(fake, cwd, Environment(fake, home, config), "--session-id", relocated.ToString("D"));
        File.Exists(Transcript(config, SeenCwd(fake), relocated)).Should().BeTrue("CLAUDE_CONFIG_DIR relocates ~/.claude, as it does for claude");
        File.Exists(Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), relocated)).Should().BeFalse("the relocated home is the only one written");
    }

    [Fact]
    public async Task A_launch_without_the_transcripts_flag_leaves_no_projects_folder()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var cwd = temp.Folder("r", "abc", "arm");
        var home = temp.Folder("home");
        fake.Rewrite(new JsonObject { ["stdoutFile"] = temp.File("stream\n", "f.ndjson") });

        await LaunchAsync(fake, cwd, Environment(fake, home), "--session-id", Guid.NewGuid().ToString("D"));
        await LaunchAsync(fake, cwd, Environment(fake, home, temp.Combine("cfg")), "--resume", Guid.NewGuid().ToString("D"));

        Directory.EnumerateDirectories(temp.Path, "projects", SearchOption.AllDirectories).Should().BeEmpty("a script that does not ask leaves the fake exactly as it was");
        fake.Calls().Should().HaveCount(2, "the launches ran — the absence is the answer, not a failure to launch");
    }

    [Fact]
    public async Task A_launch_with_the_flag_but_no_session_argument_leaves_no_projects_folder()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var home = temp.Folder("home");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = temp.File("stream\n", "f.ndjson") });

        await LaunchAsync(fake, temp.Folder("r", "abc", "arm"), Environment(fake, home));

        Directory.EnumerateDirectories(temp.Path, "projects", SearchOption.AllDirectories).Should().BeEmpty("a launch with no session id has no transcript to leave");
        fake.Calls().Should().HaveCount(1);
    }
}
