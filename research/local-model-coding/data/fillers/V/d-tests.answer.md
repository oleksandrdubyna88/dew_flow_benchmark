```csharp
namespace CoaiMcp.Tests;

[Collection("fakecli-env")]
public sealed class AKimiConsultantCheckTests : IDisposable
{
    private readonly string _data = Directory.CreateTempSubdirectory("coai-kcheck-data-").FullName;
    private readonly string _temp = Directory.CreateTempSubdirectory("coai-kcheck-temp-").FullName;
    private readonly List<string> _warned = new();
    private readonly List<string> _unlinked = new();

    public AKimiConsultantCheckTests()
    {
        GitScratch.AfterUnlink = _unlinked.Add;
    }

    public void Dispose()
    {
        GitScratch.AfterUnlink = null;
        try { Directory.Delete(_temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        try { Directory.Delete(_data, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Task<(int Code, JsonElement Said, string Err)> Check(PanelSettings settings, IProcessLauncher launcher, CancellationToken ct, params string[] args)
    {
        return ConsultantCheckMode.AnswerAsync(
            settings, ["--check-consultant", .. args], launcher, _temp, _warned.Add, Noticing.None, ct)
            .ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully) throw t.Exception!;
                var (code, stdout, stderr) = t.Result;
                return (code, stdout.Length > 0 ? JsonDocument.Parse(stdout).RootElement.Clone() : default, stderr);
            }, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? string.Empty;

    private static PanelSettings Settings(string homesRoot) => new()
    {
        Providers = [],
        DataDir = _data,
        ReviewerTimeout = TimeSpan.FromSeconds(60),
        ConsultEnabled = true,
        ConsultantHomesRoot = homesRoot,
        Consultants = new Dictionary<string, ConsultantChoice>
        {
            [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi"),
        },
    };

    private static Scopes StandIn(KimiFixture person)
    {
        return new Scopes(
            WindowsStandIn.Begin(),
            KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")),
            KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, []))));
    }

    private sealed class Scopes(params IDisposable[] scopes) : IDisposable
    {
        public void Dispose()
        {
            for (var i = scopes.Length - 1; i >= 0; i--) scopes[i].Dispose();
        }
    }

    private static string Answer(ProcessRequest request)
    {
        var cwd = request.Arguments.FirstOrDefault(a => a == "--cwd" || a.StartsWith("--cwd="))?.Split('=')[1];
        var parts = cwd?.Split(' ').LastOrDefault()?.TrimEnd('.');
        return $"marker: {parts}{Environment.NewLine}canary: CANNOT";
    }

    [Fact]
    public async Task AKimiCheck_RunsInACopy_OnAOneShotHome()
    {
        using var homes = TempDir.For("testsToDelete-vh-check-");
        using var person = new KimiFixture();
        using var _ = StandIn(person);

        var played = new PlayedKimi(new ProcessLauncher())
        {
            Turn = (_, request) => PlayedKimi.Answered(PlayedKimi.SessionOf(request), 100, 10, Answer(request)),
        };

        var settings = Settings(homes) with
        {
            Consultants = new Dictionary<string, ConsultantChoice>
            {
                [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi"),
            },
        };

        var (code, said, _) = await Check(settings, played, TestContext.Current.CancellationToken, "--caller", "claude");

        code.Should().Be(0);
        Text(said, "state").Should().Be("answered", said.ToString());
        said.GetProperty("markerRead").GetBoolean().Should().BeTrue();

        played.Shims.Count.Should().Be(1);
        var launch = played.Shims.Dequeue();
        var cwdArg = launch.Request.Arguments.FirstOrDefault(a => a == "--cwd" || a.StartsWith("--cwd="))?.Split('=')[1];
        cwdArg.Should().Contain(Path.Combine("own", "tree"));

        var homeArg = launch.Request.Arguments.FirstOrDefault(a => a == "--home" || a.StartsWith("--home="))?.Split('=')[1];
        homeArg.Should().StartWith(Path.Combine(homes, "coai-vh") + Path.DirectorySeparatorChar);
        var homeName = Path.GetFileName(homeArg);
        homeName.Should().StartWith("kh-");
        homeName.Length.Should().Be(13);

        Directory.EnumerateDirectories(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AfterTheCheck_TheHomeIsGone_JunctionFirst(bool failTurn)
    {
        using var homes = TempDir.For("testsToDelete-vh-check-");
        using var person = new KimiFixture();
        using var _ = StandIn(person);

        var played = new PlayedKimi(new ProcessLauncher())
        {
            Turn = (_, request) => failTurn
                ? PlayedKimi.Lost(PlayedKimi.SessionOf(request))
                : PlayedKimi.Answered(PlayedKimi.SessionOf(request), 100, 10, Answer(request)),
        };

        var settings = Settings(homes) with
        {
            Consultants = new Dictionary<string, ConsultantChoice>
            {
                [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi"),
            },
        };

        var before = person.Hash();
        var (code, said, _) = await Check(settings, played, TestContext.Current.CancellationToken, "--caller", "claude");

        var entries = Directory.Exists(Path.Combine(homes, "coai-vh"))
            ? Directory.EnumerateFileSystemEntries(Path.Combine(homes, "coai-vf"))
            : Array.Empty<string>();
        entries.Should().BeEmpty();
        _unlinked.Should().BeEmpty();
        person.Hash().Should().Equal(before);

        if (failTurn)
        {
            Text(said, "state").Should().NotBe("answered");
        }
    }

    [Fact]
    public async Task ACheckCancelledMidTurn_StillDisposesItsHome()
    {
        using var homes = TempDir.For("testsToDelete-vh-check-");
        using var person = new KimiFixture();
        using var _ = StandIn(person);

        var tcs = new TaskCompletionSource();
        var played = new PlayedKimi(new ProcessLauncher())
        {
            During = async (_, ct) =>
            {
                tcs.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            },
            Turn = (_, request) => PlayedKimi.Answered(PlayedKimi.SessionOf(request), 100, 10, Answer(request)),
        };

        var settings = Settings(homes) with
        {
            Consultants = new Dictionary<string, ConsultantChoice>
            {
                [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi"),
            },
        };

        using var stop = new CancellationTokenSource();
        var checkTask = Check(settings, played, stop.Token, "--caller", "claude");

        await tcs.Task;
        await stop.CancelAsync();

        try
        {
            await checkTask;
        }
        catch (OperationCanceledException) { }

        var entries = Directory.Exists(Path.Combine(homes, "coai-vf"))
            ? Directory.EnumerateFileSystemEntries(Path.Combine(homes, "coai-vf"))
            : Array.Empty<string>();
        entries.Should().BeEmpty();
        _unlinked.Should().BeEmpty();
        person.Hash().Should().Equal(person.Hash());
    }

    [Fact]
    public async Task ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst()
    {
        using var person = new KimiFixture();
        using var _ = StandIn(person);

        string? recordedHome = null;
        var played = new PlayedKimi(new ProcessLauncher())
        {
            Turn = (_, request) =>
            {
                var home = request.Arguments.FirstOrDefault(a => a == "--home" || a.StartsWith("--home="))?.Split('=')[1];
                recordedHome = home;
                return PlayedKimi.Answered(PlayedKimi.SessionOf(request), 100, 10, Answer(request));
            },
        };

        var settings = Settings(string.Empty) with
        {
            Consultants = new Dictionary<string, ConsultantChoice>
            {
                [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi"),
            },
        };

        var (code, _, _) = await Check(settings, played, TestContext.Current.CancellationToken, "--caller", "claude");

        code.Should().Be(0);
        recordedHome.Should().Contain(Path.Combine("own", "kh-"));
        var parent = Path.GetDirectoryName(recordedHome);
        parent.Should().NotBeNull();
        Directory.Exists(parent).Should().BeFalse();

        Directory.EnumerateDirectories(_temp, "coai-check-*").Should().BeEmpty();
        _unlinked.Should().BeEmpty();
    }
}
```