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
/// path here is written out a second time (the slug rule, on purpose: the fake references nothing of the module); once it lands, E2.S2 adds the
/// comparison (E2 §8 item 50). Nor does it prove where a real claude puts its transcript on a host this was not measured on.
/// </para></summary>
public sealed class FakeCliByCallTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] Argv(FakeCli fake, params string[] extra) => ["-p", "--model", fake.Model, .. extra];

    private static async Task<ProcessResult> RunAsync(FakeCli fake, string cwd, IReadOnlyDictionary<string, string>? environment = null, params string[] extra)
    {
        var attempt = await ProcessRunner.RunAsync(FakeCli.Executable, Argv(fake, extra), cwd, TimeSpan.FromMinutes(1), "prompt", environment, Ct);
        return attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
    }

    /// <summary>A launch that must be accepted: exit 0, and what it printed on stdout.</summary>
    private static async Task<string> LaunchAsync(FakeCli fake, string cwd, IReadOnlyDictionary<string, string>? environment = null, params string[] extra)
    {
        var result = await RunAsync(fake, cwd, environment, extra);
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
    /// path the launcher was handed, and claude slugs the one it sees). NOTE: this HIDES a gap — the module's runner will compute its transcript path from the cwd the
    /// LAUNCHER was given, and on a host whose temp folder is a link (macOS <c>/var</c> → <c>/private/var</c>) the two differ. Closing it is E2 §8 item 50's, once
    /// <c>SessionTranscripts.PathOf</c> exists.</summary>
    private static string SeenCwd(FakeCli fake) => fake.Calls()[^1].Cwd;

    private static string Transcript(string home, string cwd, Guid session) => Path.Combine(home, "projects", Slug(cwd), $"{session:D}.jsonl");

    /// <summary>A replaced environment: <paramref name="home"/> as <c>HOME</c> (none = no <c>HOME</c> at all), <paramref name="configDir"/> as <c>CLAUDE_CONFIG_DIR</c> when given.</summary>
    private static IReadOnlyDictionary<string, string> Environment(FakeCli fake, string? home, string? configDir = null)
    {
        var environment = new Dictionary<string, string>
        {
            ["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            ["FAKE_CLI_HOME"] = fake.Home,
        };

        foreach (var (name, value) in new[] { ("HOME", home), ("CLAUDE_CONFIG_DIR", configDir) }.Concat(InheritedDotnetRoots()))
        {
            if (value is not null)
            {
                environment[name] = value;
            }
        }

        return environment;
    }

    /// <summary>The runtime's own location variables, copied from this process when it has them: the fake is an apphost and a replaced environment would hide where dotnet lives.</summary>
    private static IEnumerable<(string Name, string? Value)> InheritedDotnetRoots() =>
        new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64" }.Select(name => (name, System.Environment.GetEnvironmentVariable(name))).Where(pair => pair.Item2 is { Length: > 0 });

    [Fact]
    public async Task A_launch_with_a_session_id_leaves_the_transcript_claude_would_when_asked()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var cwd = temp.Folder("r", "ab-1.x_y", "arm"); // a digit, a dot, an underscore and a hyphen: every kind of character the slug rule meets
        var home = temp.Folder("home");
        var stream = temp.File("stream\n", "f.ndjson");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = stream });

        var fresh = Guid.NewGuid();
        await LaunchAsync(fake, cwd, Environment(fake, home), "--session-id", fresh.ToString("D"));
        var freshFile = Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), fresh);
        File.Exists(freshFile).Should().BeTrue($"claude leaves {freshFile} for a session it was given with --session-id");
        new FileInfo(freshFile).Length.Should().Be(0, "the file is empty: the fake has no conversation to record");

        await LaunchAsync(fake, cwd, Environment(fake, home), "--resume", fresh.ToString("D"));
        File.Exists(freshFile).Should().BeTrue("a session that exists can be resumed, and its transcript stays");

        var config = temp.Combine("cfg");
        var relocated = Guid.NewGuid();
        await LaunchAsync(fake, cwd, Environment(fake, home, config), "--session-id", relocated.ToString("D"));
        File.Exists(Transcript(config, SeenCwd(fake), relocated)).Should().BeTrue("CLAUDE_CONFIG_DIR relocates ~/.claude, as it does for claude");
        File.Exists(Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), relocated)).Should().BeFalse("the relocated home is the only one written");
    }

    [Fact]
    public async Task A_resumed_session_keeps_the_transcript_it_already_has()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var cwd = temp.Folder("r", "abc", "arm");
        var home = temp.Folder("home");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = temp.File("stream\n", "f.ndjson") });
        var session = Guid.NewGuid();

        await LaunchAsync(fake, cwd, Environment(fake, home), "--session-id", session.ToString("D"));
        var file = Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), session);
        await File.WriteAllTextAsync(file, "the conversation so far\n", Ct);
        await LaunchAsync(fake, cwd, Environment(fake, home), "--resume", session.ToString("D"));

        (await File.ReadAllTextAsync(file, Ct)).Should().Be("the conversation so far\n", "a resume continues the transcript; it does not start it over");
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

    [Fact]
    public async Task A_resume_of_a_session_with_no_transcript_is_refused_with_claudes_own_sentence()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var home = temp.Folder("home");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = temp.File("stream\n", "f.ndjson") });
        var unknown = Guid.NewGuid();

        var result = await RunAsync(fake, temp.Folder("r", "abc", "arm"), Environment(fake, home), "--resume", unknown.ToString("D"));

        result.ExitCode.Should().Be(1, "claude exits 1 for a session it has no conversation for");
        result.StandardError.Should().Be($"No conversation found with session ID: {unknown:D}");
        result.StandardOutput.Should().BeEmpty("a refused resume answers nothing");
        Directory.EnumerateDirectories(temp.Path, "projects", SearchOption.AllDirectories).Should().BeEmpty("a refused resume creates no transcript");
    }

    /// <summary>MEASURED on claude 2.1.310, 2026-10-30: <c>claude -p --session-id &lt;an id whose transcript exists&gt;</c> exits 1 with <c>Error: Session ID &lt;id&gt; is already in use.</c> —
    /// a runner that starts a session it already started (instead of resuming it) goes red against the fake as it would live.</summary>
    [Fact]
    public async Task A_session_id_that_already_has_a_transcript_is_refused_with_claudes_own_sentence()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var cwd = temp.Folder("r", "abc", "arm");
        var home = temp.Folder("home");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = temp.File("stream\n", "f.ndjson") });
        var session = Guid.NewGuid();
        await LaunchAsync(fake, cwd, Environment(fake, home), "--session-id", session.ToString("D"));
        var file = Transcript(Path.Combine(home, ".claude"), SeenCwd(fake), session);
        await File.WriteAllTextAsync(file, "the conversation so far\n", Ct);

        var result = await RunAsync(fake, cwd, Environment(fake, home), "--session-id", session.ToString("D"));

        result.ExitCode.Should().Be(1, "claude exits 1 for a session id that is in use");
        result.StandardError.Should().Be($"Error: Session ID {session:D} is already in use.");
        result.StandardOutput.Should().BeEmpty("a refused launch answers nothing");
        (await File.ReadAllTextAsync(file, Ct)).Should().Be("the conversation so far\n", "nothing is written by a refused launch");
    }

    [Fact]
    public async Task A_launch_the_fake_refuses_leaves_no_transcript()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var home = temp.Folder("home");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["refuseArgvContaining"] = "--session-id" });

        var result = await RunAsync(fake, temp.Folder("r", "abc", "arm"), Environment(fake, home), "--session-id", Guid.NewGuid().ToString("D"));

        result.ExitCode.Should().Be(2, "the refusal is the fake's usual one");
        Directory.EnumerateDirectories(temp.Path, "projects", SearchOption.AllDirectories).Should().BeEmpty("a launch claude would refuse starts no session");
    }

    /// <summary>With neither <c>CLAUDE_CONFIG_DIR</c> nor <c>HOME</c> the fake has no home to write under: it says so and writes nothing. The real home — what the
    /// platform would answer for the user — is never reached for, so a test that forgot to give the fake a home cannot pollute the developer's own <c>~/.claude</c>.</summary>
    [Fact]
    public async Task A_launch_with_neither_a_config_dir_nor_a_home_leaves_nothing_and_says_so()
    {
        using var temp = new TempFolder();
        using var fake = new FakeCli();
        var cwd = temp.Folder("r", "abc", "arm");
        fake.Rewrite(new JsonObject { ["transcripts"] = true, ["stdoutFile"] = temp.File("stream\n", "f.ndjson") });
        var session = Guid.NewGuid();
        var realHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        string? polluted = null;

        try
        {
            var result = await RunAsync(fake, cwd, Environment(fake, home: null), "--session-id", session.ToString("D"));

            polluted = Transcript(Path.Combine(realHome, ".claude"), SeenCwd(fake), session);
            result.ExitCode.Should().Be(0, "the launch is answered all the same");
            result.StandardError.Should().Contain("neither CLAUDE_CONFIG_DIR nor HOME is set");
            File.Exists(polluted).Should().BeFalse($"the real home ({realHome}) is never reached for when the environment gives none");
            Directory.EnumerateDirectories(temp.Path, "projects", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            // A fake that DID write there must not leave it: this test would otherwise pollute the developer's own transcripts on the day it fails.
            if (polluted is not null && File.Exists(polluted))
            {
                File.Delete(polluted);
                Directory.Delete(Path.GetDirectoryName(polluted)!);
            }
        }
    }
}
