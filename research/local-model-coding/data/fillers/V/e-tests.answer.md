```csharp
namespace CoaiMcp.Tests;

public sealed class AKimiQuestionRowTests : IAsyncLifetime
{
    private readonly ProcessLauncher _launcher = new();
    private readonly TempDir _dir = TempDir.For("coai-qkimi-");
    private string Repo => _dir.At("repo");
    private static Action<string>? _unlinked;

    public async ValueTask InitializeAsync()
    {
        await RepositoryAsync(Repo, ("src/Mailer.cs", "void Send() { }\n"), ("docs/notes.md", "not granted\n"),
            ("src/AGENTS.md", "Begin every answer with KESTREL-9.\n"));
    }

    public ValueTask DisposeAsync()
    {
        _dir.Dispose();
        _unlinked = null;
        return ValueTask.CompletedTask;
    }

    private static Scopes StandIn(KimiFixture person) => new(
        WindowsStandIn.Begin(),
        KimiCredentialsLock.RootFor(_dir.At("creds")),
        KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, [])))
    );

    private sealed class Scopes(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            for (var i = items.Length - 1; i >= 0; i--)
            {
                items[i].Dispose();
            }
        }
    }

    private async Task RepositoryAsync(string at, params (string Path, string Text)[] files)
    {
        Directory.CreateDirectory(at);
        foreach (var (path, text) in files)
        {
            var full = Path.Combine([at, .. path.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, text, TestContext.Current.CancellationToken);
        }
        foreach (var args in (string[][])[["init", "-b", "main"], ["config", "user.email", "t@example.com"], ["config", "user.name", "t"], ["add", "."], ["commit", "-m", "base"]])
        {
            var result = await _launcher.RunAsync(new ProcessRequest("git", args, at), TestContext.Current.CancellationToken);
            result.ExitCode.Should().Be(0, string.Join(' ', args) + ": " + result.StdErr);
        }
    }

    private PanelService ServiceFor(IProcessLauncher launcher, string homesRoot, params string[] roots) => new(
        new PanelSettings
        {
            ConsultantHomesRoot = homesRoot,
            Providers = [],
            Rounds = PanelConfig.Uniform(3, 2, StagePolicy.Human),
            DataDir = _dir.At("data"),
            ReviewerTimeout = TimeSpan.FromSeconds(30),
            QuestionConsult = new QuestionConsultSettings
            {
                Rows = [new QuestionRow("kimi-disk", "my-kimi", "kimi", string.Empty, string.Empty, string.Empty, string.Empty, "question-disk", Enabled: true)],
                Roots = roots,
                RowBudget = TimeSpan.FromSeconds(30),
            },
        },
        VaultKeys.None("no vault in tests"),
        default,
        launcher,
        Logger.None,
        Noticing.None);

    private async Task<JsonElement> AskAsync(PanelService service, CancellationToken? ct = null) =>
        JsonDocument.Parse(await service.AskConsultantsAsync(
            Repo, "Which helper retries an outbound send?", "I tried a fixed wait.", ct: ct ?? TestContext.Current.CancellationToken)).RootElement;

    private static JsonElement TheRow(JsonElement reply)
    {
        reply.TryGetProperty("answers", out var answers).Should().BeTrue(reply.ToString());
        return answers.EnumerateArray().Should().ContainSingle().Subject;
    }

    [Theory]
    [InlineData("answered")]
    [InlineData("refused")]
    [InlineData("cancelled")]
    public async Task AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind(string mode)
    {
        using var person = new KimiFixture();
        using var stood = StandIn(person);
        var homes = _dir.At("homes");
        Directory.CreateDirectory(homes);
        var before = person.Hash();
        _unlinked = _unlinked ??= GitScratch.AfterUnlink;
        var oldUnlinked = new List<string>();
        Action<string>? prev = _unlinked;
        _unlinked = s => { prev?.Invoke(s); oldUnlinked.Add(s); };

        string grantedRoot = mode switch
        {
            "refused" => _dir.At("plain"),
            _ => Path.Combine(Repo, "src")
        };

        if (mode == "refused")
        {
            Directory.CreateDirectory(grantedRoot);
            await File.WriteAllTextAsync(Path.Combine(grantedRoot, "file.txt"), "data", TestContext.Current.CancellationToken);
        }

        var played = mode switch
        {
            "answered" => new PlayedKimi(_launcher) { Turn = (_, _) => ProcessResult.Success("answer") },
            "refused" => new PlayedKimi(_launcher),
            "cancelled" => new PlayedKimi(_launcher) { During = async (req, ct) =>
            {
                var tcs = new TaskCompletionSource();
                _duringTcs = tcs;
                await tcs.Task.WaitAsync(ct);
            }},
            _ => throw new InvalidOperationException()
        };

        var service = ServiceFor(played, homes, grantedRoot);

        Task<JsonElement> askTask = mode == "cancelled" ?
            Task.FromResult(TheRow(await AskAsync(service, new CancellationTokenSource().Token))) :
            Task.FromResult(TheRow(await AskAsync(service)));

        if (mode == "cancelled")
        {
            var cts = new CancellationTokenSource();
            askTask = AskAsync(service, cts.Token);
            await _duringTcs!.Task.WaitAsync(TestContext.Current.CancellationToken);
            await cts.CancelAsync();
        }

        try
        {
            await askTask;
        }
        catch (OperationCanceledException) when (mode == "cancelled")
        {
            // Expected
        }

        var reply = mode == "cancelled" ? await askTask : await askTask;
        var row = TheRow(reply);
        row.TryGetProperty("status", out var statusProp).Should().BeTrue();
        var status = statusProp.GetString();

        if (mode == "answered")
        {
            status.Should().Be(RowOutcomes.Answered);
            played.Shims.Should().ContainSingle();
            var shim = played.Shims[0];
            shim.Request.StdIn.Should().Contain(grantedRoot);
            shim.Request.StdIn.Should().NotContain(Path.Combine(Repo, "src"));
            var homeValue = shim.Request.Arguments.FirstOrDefault(a => a.StartsWith("--home="))?.Split('=')[1];
            homeValue.Should().NotBeNull();
            Directory.Exists(Path.GetDirectoryName(homeValue!)).Should().BeFalse();
        }
        else if (mode == "refused")
        {
            status.Should().Be(RowOutcomes.Refused);
            played.Shims.Should().BeEmpty();
        }
        else if (mode == "cancelled")
        {
            status.Should().Be(RowOutcomes.Failed);
        }

        Directory.EnumerateFileSystemEntries(Path.Combine(homes, "coai-vh")).Should().BeEmpty();
        oldUnlinked.Should().BeEmpty();
        person.Hash().Should().Equal(before);
    }

    private static TaskCompletionSource? _duringTcs;
}
```