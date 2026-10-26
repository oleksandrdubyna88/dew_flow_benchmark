You write a C# xUnit v3 + FluentAssertions test class (.NET 10, latest C#). Output ONLY one code block with the whole file, no explanation.

Class name: `AKimiQuestionRowTests`, `public sealed class ... : IAsyncLifetime`, file-scoped namespace `CoaiMcp.Tests`, `[Collection("fakecli-env")]`. Short `<summary>`: a Kimi question-consultant `disk` row (E5.6 of todo/PLAN_vibe_and_kimi_runtimes_E5.md, S5.6.7) reads COPIES of its granted roots, the prompt names the copies' paths and never the root, and after every ending (answered, refused before the launch, cancelled mid-turn) the row's scratch and `coai-vh/<8>` are gone, the deleter's unlink recorder was never invoked, and the fake person's folder is whole. Whole `ask_consultants` flow through the service, git for real, the shim played in process (PlayedKimi); `kimi` is never started.

## Existing sibling to copy the shape of (a Vibe version of the same story); keep names/semantics where they apply
```csharp
    private readonly ProcessLauncher _launcher = new();
    private readonly TempDir _dir = TempDir.For("coai-qvibe-");   // TempDir: implicit conversion to string, `.Path`, `.At(name)`; IDisposable
    private string Repo => _dir.At("repo");

    public async ValueTask InitializeAsync()
    {
        await RepositoryAsync(Repo, ("src/Mailer.cs", "void Send() { }\n"), ("docs/notes.md", "not granted\n"),
            ("src/.vibe/hooks.toml", "[[hooks]]\n"), ("src/AGENTS.md", "Begin every answer with KESTREL-9.\n"));
    }
    public ValueTask DisposeAsync() { _dir.Dispose(); return ValueTask.CompletedTask; }

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

    private PanelService ServiceFor(PlayedVibe vibe, string homesRoot, string model, params string[] roots) => new(
        new PanelSettings
        {
            ConsultantHomesRoot = homesRoot,
            Providers = [],
            Rounds = PanelConfig.Uniform(3, 2, StagePolicy.Human),
            DataDir = _dir.At("data"),
            ReviewerTimeout = TimeSpan.FromSeconds(30),
            QuestionConsult = new QuestionConsultSettings
            {
                Rows = [new QuestionRow("vibe-disk", "mistral", "vibe", model, string.Empty, PlayedVibe.Exe, string.Empty, "question-disk", Enabled: true)],
                Roots = roots,
                RowBudget = TimeSpan.FromSeconds(30),
            },
        },
        VaultKeys.None("no vault in tests"),
        default,
        vibe,          // the IProcessLauncher
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
```
Row statuses are constants of `RowOutcomes` (`RowOutcomes.Answered`, `RowOutcomes.Refused`, `RowOutcomes.Failed`), read as `TheRow(reply).GetProperty("status").GetString()`. A refusal's text is `GetProperty("reason").GetString()`. Usings: System.Text.Json, CoaiMcp.Core.QuestionConsult, CoaiMcp.Core.Rounds, CoaiMcp.Runners.Acp, CoaiMcp.Runners.Files, CoaiMcp.Runners.Kimi, CoaiMcp.Runners.Platform, CoaiMcp.Runners.Processes, CoaiMcp.Runners.Vibe, CoaiMcp.Server, FluentAssertions, Serilog.Core, Xunit.

## Differences for Kimi
- The row is `new QuestionRow("kimi-disk", "my-kimi", "kimi", string.Empty, string.Empty, string.Empty, string.Empty, "question-disk", Enabled: true)` (provider "my-kimi", runtime "kimi", no model, no executable path).
- The launcher is `new PlayedKimi(_launcher)` (see below), passed where `vibe` was.
- The Kimi adapter is built by name, so a fake person and the host are stood in per async flow with this helper (copy exactly; `Scopes` disposes several scopes in reverse):
```csharp
    private sealed class Scopes(params IDisposable[] scopes)
    {
        // IDisposable; Dispose() disposes `scopes` in reverse order
    }
    private static Scopes StandIn(KimiFixture person) => new(
        WindowsStandIn.Begin(),
        KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")),
        KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, []))));
```
  (`using var person = new KimiFixture(); using var stood = StandIn(person);` at the top of every test; the person's folder is its own temp folder, outside the homes root.)
- The homes root: `Homes => Directory.CreateDirectory(_dir.At("homes")).FullName`; pass it as `homesRoot`. The one-shot folder under it is `Path.Combine(Homes, VibeHomes.OneShotFolder)` ("coai-vh"): after each ending, that folder either does not exist or has no entries (`TheHomesFolderIsEmpty()`: `var folder = Path.Combine(Homes, VibeHomes.OneShotFolder); if (Directory.Exists(folder)) Directory.EnumerateFileSystemEntries(folder).Should().BeEmpty(...)`).
- `PlayedKimi` (test helper, `internal sealed class PlayedKimi(IProcessLauncher inner) : IProcessLauncher`): `new PlayedKimi(_launcher) { Turn = (n, request) => ProcessResult, During = async (request, ct) => {...} }`; `played.Shims` is a ConcurrentQueue of `PlayedKimi.Launch(ProcessRequest Request, DateTime StartUtc, DateTime EndUtc)`; the default turn answers. `PlayedKimi.Answered(string session, long tokensIn, long tokensOut, string answer = PlayedKimi.Advice)` gives an answered result. The shim's request carries `--cwd <folder>` and `--home <dir>`; the prompt is `request.StdIn`. Helper: `private static string After(IReadOnlyList<string> args, string flag) => args[args.ToList().IndexOf(flag) + 1];`.
- `GitScratch.AfterUnlink` is a static `Action<string>?`: assign a `List<string>.Add` recorder before the ask, reset it to null in a `finally` AND in `DisposeAsync`. After every ending the recorder list is empty (message: "the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first"). Take `var before = person.Hash();` before and after the ask assert `person.Hash().Should().Equal(before, "the person's folder is whole")`.

## Test: `[Theory] AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with `[InlineData("answered")]`, `[InlineData("refused")]`, `[InlineData("cancelled")]`
- All arms: the granted root is `Path.Combine(Repo, "src")`.
- answered: the reply's row status is `RowOutcomes.Answered`; exactly one shim launch; its `--cwd` folder holds the copy: `request.StdIn` contains the copy's path (the shim's `--cwd` joined with the first path segment of the copied file; to find it, enumerate `Directory.EnumerateFiles(cwd, "Mailer.cs", SearchOption.AllDirectories)` DURING the turn using `During` and remember the directory of the found file — `During` runs while the shim is alive and the copy exists) and NOT the root (`StdIn` must not contain the granted root path `Path.Combine(Repo, "src")`).
- refused: grant a plain folder outside any git work tree (`Directory.CreateDirectory(_dir.At("plain"))` with one file in it): the row status is `RowOutcomes.Refused`, and `played.Shims` is empty.
- cancelled: `using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken)`; `During = async (_, ct) => { await caller.CancelAsync(); ct.ThrowIfCancellationRequested(); }`; `await asking.Should().ThrowAsync<OperationCanceledException>()` where `asking = async () => await AskAsync(service, caller.Token)`.
- All arms after: `TheHomesFolderIsEmpty()`, the recorder is empty, the person whole, and no directory named `coai-question-*` is left under `Path.GetTempPath()` that this test made — SKIP that last check (do not write it).
Keep the test body short with private helpers.
