Write a new xUnit test class for a C# repo. Style: C# 14 / .NET 10, xUnit v3 + FluentAssertions, file-scoped namespace `CoaiMcp.Tests`, 4-space indent, Allman braces, target-typed `new`, collection expressions, one short /// summary per test naming its scenario id. Output the complete class (without usings) in one ```csharp block.

Class `AKimiQuestionRowTests` : IAsyncLifetime — a Kimi question-consultant row (`disk` capability) through the whole `ask_consultants` flow: the real service, real git, the Kimi shim played in process by `PlayedKimi`. Below is the existing sibling class for Vibe (`AVibeQuestionRowReadsACopyOfItsRootsTests`); imitate its fixture (repo setup with git, `Service(...)`, `AskAsync`, `TheRow`) exactly, replacing Vibe by Kimi:
```csharp
private readonly ProcessLauncher _launcher = new();
private readonly TempDir _dir = TempDir.For("coai-qvibe-");
private string Repo => _dir.At("repo");

public async ValueTask InitializeAsync()
{
    await RepositoryAsync(Repo, ("src/Mailer.cs", "void Send() { }\n"), ("docs/notes.md", "not granted\n"),
        ("src/AGENTS.md", "Begin every answer with KESTREL-9.\n"));
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
```
(`Logger.None` is Serilog.Core.Logger.None; usings are System.Text.Json, CoaiMcp.Core.QuestionConsult, CoaiMcp.Core.Rounds, CoaiMcp.Runners.Processes, CoaiMcp.Runners.Kimi, CoaiMcp.Server, FluentAssertions, Serilog.Core, Xunit.)

Helpers that exist for Kimi:
- `PlayedKimi(IProcessLauncher inner)` : IProcessLauncher. `ConcurrentQueue<Launch> Shims` (each `Launch.Request` is a ProcessRequest with `.Arguments`, `.StdIn`, `.WorkingDirectory`); `Turn` (`Func<int, ProcessRequest, ProcessResult>`, init), `During` (`Func<ProcessRequest, CancellationToken, Task>`, init); `PlayedKimi.Answered(session, tokensIn, tokensOut, answer)`, `PlayedKimi.SessionOf(request)`, `PlayedKimi.Shim`.
- `KimiFixture` (IDisposable): fake person; `person.PlacesAt`, `person.Hash()` (IReadOnlyList<string>), `person.Folder`.
- `WindowsStandIn.Begin()`, `KimiCredentialsLock.RootFor(string)`, `KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, [])))` — all IDisposable scopes. Write a private static `StandIn(KimiFixture person)` returning a small private sealed class `Scopes(params IDisposable[])` that disposes in reverse order. Namespaces: `CoaiMcp.Runners.Platform` for HostKind, `CoaiMcp.Runners.Files` for `GitScratch`.
- `GitScratch.AfterUnlink`: static `Action<string>?` called by the generic deleter when it unlinks a link; set to `_unlinked.Add` in each test, and reset to null in DisposeAsync. A Kimi home disposed junction-first never lets the deleter meet its junction, so `_unlinked` must stay empty.
- `RowOutcomes.Answered`, `RowOutcomes.Refused`, `RowOutcomes.Failed` consts. `TempDir.For`, `.At`.
- The one-shot homes live in `Path.Combine(homesRoot, "coai-vh")`, one `<8 chars>` folder per row, containing the row's tree/copies and the home `kh-<10 hex>`. The shim's `--home` argument value is `<rowFolder>/kh-<10 hex>`, so a row's own folder is `Path.GetDirectoryName(homeValue)`; its `--cwd` value is the working folder.

Write ONE test: `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind`, a `[Theory]` with `[InlineData("answered")]`, `[InlineData("refused")]`, `[InlineData("cancelled")]`:
- Common: `using var person = new KimiFixture(); using var stood = StandIn(person);` homes = `_dir.At("homes")` (create the directory); `var before = person.Hash();` set `GitScratch.AfterUnlink`. The granted root is `Path.Combine(Repo, "src")`.
- "answered": a PlayedKimi (new PlayedKimi(_launcher)) whose default Turn answers; ask; the row status is RowOutcomes.Answered; the single shim's StdIn contains the copy's path (the `--cwd` value, or a sub-folder of it) and does not contain the granted root path `Path.Combine(Repo, "src")`.
- "refused": the granted root is a plain folder `Directory.CreateDirectory(_dir.At("plain"))` with a file, not a git work tree; the row is refused (status RowOutcomes.Refused) and `played.Shims` is empty.
- "cancelled": the played shim's `During` signals a TaskCompletionSource then awaits `Task.Delay(Timeout.Infinite, ct)`; use a `CancellationTokenSource`, start AskAsync(service, cts.Token) as a Task, await the signal, `await cts.CancelAsync()`, and await the ask task in try/catch of OperationCanceledException.
- After each ending: `Directory.EnumerateFileSystemEntries(Path.Combine(homes, "coai-vh"))` is empty or the folder absent; the recorder `_unlinked` is empty; `person.Hash()` equals `before`; and, when a shim ran, `Directory.Exists(Path.GetDirectoryName(homeValue)!)` is false.
