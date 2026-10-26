

```csharp
using System.Text.Json;
using CoaiMcp.Runners.Acp;
using CoaiMcp.Runners.Files;
using CoaiMcp.Runners.Kimi;
using CoaiMcp.Runners.Platform;
using CoaiMcp.Runners.Processes;
using CoaiMcp.Runners.Vibe;
using CoaiMcp.Server;
using FluentAssertions;
using Xunit;

namespace CoaiMcp.Tests;

/// <summary>
/// a Kimi consultant check (E5.5 of todo/PLAN_vibe_and_kimi_runtimes_E5.md) runs a turn as a consultation does:
/// in a COPY of the check's scratch repository, on a ONE-SHOT owned home under &lt;root&gt;/coai-vh/&lt;8&gt;/kh-&lt;10 hex&gt;,
/// disposed junction-first whatever ended the check; only the shim is played (PlayedKimi), `kimi` is never started.
/// </summary>
[Collection("fakecli-env")]
public sealed class AKimiConsultantCheckTests : IDisposable
{
    private readonly string _data = Directory.CreateTempSubdirectory("coai-checktest-data-").FullName;
    private readonly string _temp = Directory.CreateTempSubdirectory("coai-checktest-temp-").FullName;
    private readonly List<string> _warned = [];
    private KimiFixture _person = new();

    public void Dispose()
    {
        GitScratch.AfterUnlink = null;
        _person?.Dispose();
        try { Directory.Delete(_data, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        try { Directory.Delete(_temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Scopes(params IDisposable[] scopes) : IDisposable
    {
        public void Dispose() { foreach (var scope in scopes.Reverse()) { scope.Dispose(); } }
    }

    private static Scopes StandIn(KimiFixture person) => new(
        WindowsStandIn.Begin(),
        KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")),
        KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, []))));

    private PanelSettings Settings(string runtime = "codex", TimeSpan? timeout = null, bool enabled = true) => new()
    {
        Providers = [],
        DataDir = _data,
        ReviewerTimeout = timeout ?? TimeSpan.FromSeconds(60),
        ConsultEnabled = enabled,
        Consultants = new Dictionary<string, ConsultantChoice>
        {
            [CallerIdentity.Claude] = new(runtime, Runtime: runtime, ExecutablePath: FakeCliExe),
        },
    };

    private async Task<(int Code, JsonElement Said, string Err)> Check(PanelSettings settings, IProcessLauncher launcher, CancellationToken ct, params string[] args)
    {
        var (code, stdout, stderr) = await ConsultantCheckMode.AnswerAsync(
            settings, ["--check-consultant", .. args], launcher, _temp, _warned.Add, Noticing.None, ct);
        return (code, stdout.Length > 0 ? JsonDocument.Parse(stdout).RootElement.Clone() : default, stderr);
    }

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? string.Empty;
    private static string After(IReadOnlyList<string> args, string flag) => args[args.ToList().IndexOf(flag) + 1];

    private static PlayedKimi Played(Func<int, ProcessRequest, ProcessResult>? turnOverride = null, Action<ProcessRequest, CancellationToken>? during = null)
    {
        var played = new PlayedKimi(new ProcessLauncher()) { During = during };
        played.Turn = (n, request) =>
        {
            if (turnOverride != null) return turnOverride(n, request);
            var cwd = After(request.Arguments, "--cwd");
            var text = File.ReadAllText(Path.Combine(cwd, "CHECK.md"));
            var marker = text.Trim().Split(' ')[^1].TrimEnd('.');
            return PlayedKimi.Answered(PlayedKimi.SessionOf(request), 20000, 30, $"marker: {marker}{Environment.NewLine}canary: CANNOT");
        };
        return played;
    }

    [Fact]
    public async Task AKimiCheck_RunsInACopy_OnAOneShotHome()
    {
        try
        {
            using var homes = TempDir.For("testsToDelete-kimi-check-");
            using var person = new KimiFixture();
            using var _ = StandIn(person);
            var settings = Settings("my-kimi") with
            {
                ConsultantHomesRoot = homes,
                Consultants = new Dictionary<string, ConsultantChoice>
                {
                    [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi", ExecutablePath: FakeCliExe),
                },
            };
            var played = Played();
            var (code, said, _) = await Check(settings, played, TestContext.Current.CancellationToken, "--caller", "claude");
            code.Should().Be(0);
            Text(said, "state").Should().Be("answered", said.ToString());
            said.GetProperty("markerRead").GetBoolean().Should().BeTrue("the copy holds the scratch's CHECK.md");

            played.Shims.Should().HaveCount(1);
            var req = played.Shims.First();
            After(req.Arguments, "--cwd").Should().Contain(Path.Combine("own", "tree"));
            var home = After(req.Arguments, AcpWords.HomeFlag);
            Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(home))).Should().Be("coai-vh");
            Path.GetFileName(Path.GetDirectoryName(home)).Length.Should().Be(8);
            Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
            home.StartsWith(homes).Should().BeTrue();

            Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
        }
        finally { GitScratch.AfterUnlink = null; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AfterTheCheck_TheHomeIsGone_JunctionFirst(bool failTurn)
    {
        try
        {
            using var homes = TempDir.For("testsToDelete-kimi-check-");
            using var person = new KimiFixture();
            using var _ = StandIn(person);
            var settings = Settings("my-kimi") with
            {
                ConsultantHomesRoot = homes,
                Consultants = new Dictionary<string, ConsultantChoice>
                {
                    [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi", ExecutablePath: FakeCliExe),
                },
            };
            var recorded = new List<string>();
            GitScratch.AfterUnlink = recorded.Add;
            var initialHash = person.Hash();

            PlayedKimi played;
            if (failTurn)
            {
                played = Played(n => n == 0 ? new ProcessResult(70, string.Empty, "the agent failed", TimedOut: false) : default!);
            }
            else
            {
                played = Played();
            }

            var (code, _, _) = await Check(settings, played, TestContext.Current.CancellationToken, "--caller", "claude");

            Directory.EnumerateFileSystemEntries(Path.Combine(homes, VibeHomes.OneShotFolder)).Should().BeEmpty();
            recorded.Should().BeEmpty("the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first");
            person.Hash().Should().Equal(initialHash);

            if (!failTurn)
            {
                code.Should().Be(0);
            }
        }
        finally { GitScratch.AfterUnlink = null; }
    }

    [Fact]
    public async Task ACheckCancelledMidTurn_StillDisposesItsHome()
    {
        try
        {
            using var homes = TempDir.For("testsToDelete-kimi-check-");
            using var person = new KimiFixture();
            using var _ = StandIn(person);
            var settings = Settings("my-kimi") with
            {
                ConsultantHomesRoot = homes,
                Consultants = new Dictionary<string, ConsultantChoice>
                {
                    [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi", ExecutablePath: FakeCliExe),
                },
            };
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var recorded = new List<string>();
            GitScratch.AfterUnlink = recorded.Add;
            var initialHash = person.Hash();
            JsonElement? said = null;
            try
            {
                var played = Played(during: (_, ct) => { caller.CancelAsync(); ct.ThrowIfCancellationRequested(); });
                var (code, s, _) = await Check(settings, played, caller.Token, "--caller", "claude");
                said = s;
            }
            catch (OperationCanceledException) { }

            Directory.EnumerateFileSystemEntries(Path.Combine(homes, VibeHomes.OneShotFolder)).Should().BeEmpty();
            recorded.Should().BeEmpty();
            person.Hash().Should().Equal(initialHash);
            if (said.HasValue) said.Should().NotBeNull();
        }
        finally { GitScratch.AfterUnlink = null; }
    }

    [Fact]
    public async Task ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst()
    {
        try
        {
            using var person = new KimiFixture();
            using var _ = StandIn(person);
            var settings = Settings("my-kimi") with
            {
                Consultants = new Dictionary<string, ConsultantChoice>
                {
                    [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi", ExecutablePath: FakeCliExe),
                },
            };
            var recorded = new List<string>();
            GitScratch.AfterUnlink = recorded.Add;
            var initialHash = person.Hash();

            var played = Played();
            var (code, _, _) = await Check(settings, played, TestContext.Current.CancellationToken, "--caller", "claude");

            played.Shims.Should().HaveCount(1);
            var home = After(played.Shims.First().Arguments, AcpWords.HomeFlag);
            Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
            Path.GetFileName(Path.GetDirectoryName(home)).Should().Be("own");

            Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty();
            recorded.Should().BeEmpty();
            person.Hash().Should().Equal(initialHash);
        }
        finally { GitScratch.AfterUnlink = null; }
    }
}
```