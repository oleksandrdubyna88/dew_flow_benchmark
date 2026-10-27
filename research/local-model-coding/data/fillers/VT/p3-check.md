You write a C# xUnit v3 + FluentAssertions test class (.NET 10, latest C#). Output ONLY one code block with the whole file, no explanation.

Class name: `AKimiConsultantCheckTests`, `public sealed class ... : IDisposable`, file-scoped namespace `CoaiMcp.Tests`, attribute `[Collection("fakecli-env")]`. Keep a short `<summary>` doc comment saying: a Kimi consultant check (E5.5 of todo/PLAN_vibe_and_kimi_runtimes_E5.md) runs a turn as a consultation does: in a COPY of the check's scratch repository, on a ONE-SHOT owned home under `<root>/coai-vh/<8>/kh-<10 hex>`, disposed junction-first whatever ended the check; only the shim is played (PlayedKimi), `kimi` is never started.

## Facts you may rely on (all exist; do not invent others)
Usings: System.Text.Json, CoaiMcp.Runners.Acp, CoaiMcp.Runners.Files, CoaiMcp.Runners.Kimi, CoaiMcp.Runners.Platform, CoaiMcp.Runners.Processes, CoaiMcp.Runners.Vibe, CoaiMcp.Server, FluentAssertions, Xunit.

How an existing sibling test (a Vibe check) runs the check, in class `ConsultantCheckTests` (copy this shape):
```csharp
    private readonly string _data = Directory.CreateTempSubdirectory("coai-checktest-data-").FullName;
    private readonly string _temp = Directory.CreateTempSubdirectory("coai-checktest-temp-").FullName;
    private readonly List<string> _warned = [];

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
```
and its Vibe test:
```csharp
        using var homes = TempDir.For("testsToDelete-vh-check-");   // TempDir: implicit conversion to string (the path), `.Path`, `.At(name)`
        var settings = Settings() with
        {
            ConsultantHomesRoot = homes,
            Consultants = new Dictionary<string, ConsultantChoice>
            {
                [CallerIdentity.Claude] = new("mistral", Model: "glm-5-3", Runtime: "vibe", ExecutablePath: PlayedVibe.Exe),
            },
        };
        var (code, said, _) = await Check(settings, vibe, TestContext.Current.CancellationToken, "--caller", "claude");
        code.Should().Be(0);
        Text(said, "state").Should().Be("answered", said.ToString());
        said.GetProperty("markerRead").GetBoolean().Should().BeTrue("the copy holds the scratch's CHECK.md");
        turn.Request.WorkingDirectory.Should().Contain(Path.Combine("own", "tree"), "a copy in the check's own scratch, never the repository");
        Directory.EnumerateDirectories(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
        Directory.EnumerateFileSystemEntries(Path.Combine(homes, VibeHomes.OneShotFolder)).Should().BeEmpty("the home's folder goes when the check ends");
```
The one-shot folder under the homes root is the constant `VibeHomes.OneShotFolder` (= "coai-vh"); the home of one check is `<homes>/coai-vh/<8 chars>/kh-<10 hex>`.

How a Kimi scenario stands in a fake person (copy exactly; `Scopes` is a tiny IDisposable that disposes several scopes in reverse):
```csharp
    private sealed class Scopes(params IDisposable[] scopes) : IDisposable
    {
        public void Dispose() { foreach (var scope in scopes.Reverse()) { scope.Dispose(); } }
    }

    private static Scopes StandIn(KimiFixture person) => new(
        WindowsStandIn.Begin(),
        KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")),
        KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, []))));
```
`new KimiFixture()` is IDisposable: a fake person; `person.Hash()` returns IReadOnlyList<string> of the person's folder (equal before/after means whole); `person.Folder` the fixture folder.
`PlayedKimi` (test helper, `internal sealed class PlayedKimi(IProcessLauncher inner) : IProcessLauncher`): construct `new PlayedKimi(new ProcessLauncher()) { Turn = (n, request) => ProcessResult }` — `Turn` is `Func<int, ProcessRequest, ProcessResult>` (n = 0-based shim launch index); `During` is `Func<ProcessRequest, CancellationToken, Task>` run while the shim is "alive"; `played.Shims` is a ConcurrentQueue of `PlayedKimi.Launch(ProcessRequest Request, DateTime StartUtc, DateTime EndUtc)`. Static helpers: `PlayedKimi.Answered(string session, long tokensIn, long tokensOut, string answer = PlayedKimi.Advice)` returns an answered ProcessResult, `PlayedKimi.Lost(string session)` returns exit 70 session_lost, `PlayedKimi.Minted` is the session id of a first turn, `PlayedKimi.SessionOf(request)`.
Request arguments carry `--cwd <dir>` (the working folder) and `--home <dir>` (the AcpWords.HomeFlag constant is "--home"; use `AcpWords.HomeFlag`). Write a private static helper `After(IReadOnlyList<string> args, string flag) => args[args.ToList().IndexOf(flag) + 1];` and use `After(request.Arguments, "--cwd")` for the working folder.
The scratch repository of the check has a file `CHECK.md` in its root whose LAST word (after trimming and dropping a trailing '.') is the marker; a copy of the repository lives at the shim's `--cwd` folder. The check passes when the answer text contains `marker: <that word>`. So the played turn must read `File.ReadAllText(Path.Combine(cwd, "CHECK.md"))` at the moment of the shim and answer with `$"marker: {text.Trim().Split(' ')[^1].TrimEnd('.')}{Environment.NewLine}canary: CANNOT"` using `PlayedKimi.Answered(PlayedKimi.SessionOf(request), 20000, 30, thatAnswer)`.
`GitScratch.AfterUnlink` is a static `Action<string>?` property: assign a `List<string>.Add` recorder to know when the bare deleter met a link; ALWAYS reset it to null in `Dispose()` and in a `finally`.

## Tests (each name exactly as given; use `Assert.SkipUnless(...)` NOT needed — the stand-in works on Linux)
1. `[Fact] AKimiCheck_RunsInACopy_OnAOneShotHome`: settings = `Settings() with { ConsultantHomesRoot = homes, Consultants = { claude -> new("my-kimi", Runtime: "kimi") } }` (homes = `TempDir.For("testsToDelete-kimi-check-")`, a person = `new KimiFixture()`, StandIn). Run the check with `--caller claude`, code 0, state "answered", markerRead true. One shim launch: its `--cwd` contains `Path.Combine("own", "tree")`; its `--home`'s parent's parent's name is `coai-vh`... precisely: `Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(home)))` is "coai-vh", `Path.GetFileName(Path.GetDirectoryName(home))` has length 8, and `Path.GetFileName(home)` matches regex `^kh-[0-9a-f]{10}$`, and the home starts with the homes root. No `coai-check-*` directory left in `_temp`.
2. `[Theory] AfterTheCheck_TheHomeIsGone_JunctionFirst` with `[InlineData(false)]` (the turn answers) and `[InlineData(true)]` (the turn fails: Turn returns `new ProcessResult(70, string.Empty, "the agent failed", TimedOut: false)`): record `GitScratch.AfterUnlink`; take `person.Hash()` first; after the check, `Directory.EnumerateFileSystemEntries(Path.Combine(homes, VibeHomes.OneShotFolder))` is empty, the recorder list is empty (message: "the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first"), and `person.Hash()` equals the earlier one. For the failing arm do not assert the state of the check, only the cleanup.
3. `[Fact] ACheckCancelledMidTurn_StillDisposesItsHome`: `using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);` the played shim's `During = async (_, ct) => { await caller.CancelAsync(); ct.ThrowIfCancellationRequested(); }`. Run the check with `caller.Token`. The check may either throw OperationCanceledException or return a record saying it was cancelled: wrap in try/catch of `OperationCanceledException` only, then assert (either way) that `coai-vh` holds no entries, the recorder is empty, and the person's folder is whole. If it returned, assert nothing about `said` other than that it is not null-ish (do not assert).
4. `[Fact] ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst`: settings with NO `ConsultantHomesRoot` (leave the default empty), the person and stand-in as above. After the check: the shim's `--home` contains `Path.Combine("own", "kh-")`... i.e. `Path.GetFileName(home)` matches `^kh-[0-9a-f]{10}$` and `Path.GetFileName(Path.GetDirectoryName(home))` is "own"; after the check `Directory.EnumerateDirectories(_temp, "coai-check-*")` is empty (the scratch is gone), the recorder is empty, the person whole.

Keep each test body short; share the setup in private helpers (`KimiSettings(string homesRoot)`, `Played(...)`). Dispose the fixtures: the class `Dispose()` removes `_data`, `_temp` (try/catch IOException and UnauthorizedAccessException) and resets `GitScratch.AfterUnlink = null`; create `_person = new KimiFixture()` as a field and dispose it. Use `TestContext.Current.CancellationToken`.
