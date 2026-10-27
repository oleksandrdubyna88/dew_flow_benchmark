Here's a thinking process:

1.  **Understand the Goal**: Write a single C# xUnit v3 + FluentAssertions test class for .NET 10/C# latest. The class is `AKimiQuestionRowTests`, implements `IAsyncLifetime`, uses file-scoped namespace `CoaiMcp.Tests`, and has `[Collection("fakecli-env")]`. It tests a Kimi question-consultant disk row behavior.

2.  **Analyze Requirements**:
   - Class name: `public sealed class AKimiQuestionRowTests : IAsyncLifetime`
   - Namespace: `namespace CoaiMcp.Tests;` (file-scoped)
   - Collection: `[Collection("fakecli-env")]`
   - Inherits structure from provided sibling (Vibe version) but adapts for Kimi.
   - Key differences for Kimi:
     - Row: `new QuestionRow("kimi-disk", "my-kimi", "kimi", string.Empty, string.Empty, string.Empty, string.Empty, "question-disk", Enabled: true)`
     - Launcher: `new PlayedKimi(_launcher)`
     - Scopes helper: `private sealed class Scopes(params IDisposable[] scopes) { public void Dispose() { foreach(var s in scopes.Reverse()) s.Dispose(); } }` (Wait, the prompt says `IDisposable; Dispose() disposes scopes in reverse order`. I'll implement it properly.)
     - `StandIn` helper creates `WindowsStandIn.Begin()`, `KimiCredentialsLock.RootFor(...)`, `KimiSeams.Using(...)`
     - Homes root: `Homes => Directory.CreateDirectory(_dir.At("homes")).FullName`
     - `TheHomesFolderIsEmpty()` helper checks `coai-vh` folder
     - `PlayedKimi` details: `Turn`, `During`, `Shims` queue, `Answered`, `Advice` constant. Helper `After(args, flag)`.
     - `GitScratch.AfterUnlink` static action recorder. Reset in `finally` and `DisposeAsync`.
     - `person.Hash()` before/after to check folder integrity.
   - Test: `[Theory] AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with `[InlineData("answered")]`, `[InlineData("refused")]`, `[InlineData("cancelled")]`
   - Answered arm: status `RowOutcomes.Answered`, exactly one shim launch, `StdIn` contains copy path, NOT root path. Find copy path during `During`.
   - Refused arm: grant plain folder outside git, status `RowOutcomes.Refused`, `played.Shims` empty.
   - Cancelled arm: linked token source, `During` cancels, expect `OperationCanceledException`.
   - Post-checks: `TheHomesFolderIsEmpty()`, recorder empty, person whole. Skip temp dir check.
   - Keep test body short with private helpers.

3.  **Map to Code Structure**:
   ```csharp
   using System.Text.Json;
   using CoaiMcp.Core.QuestionConsult;
   using CoaiMcp.Core.Rounds;
   using CoaiMcp.Runners.Acp;
   using CoaiMcp.Runners.Files;
   using CoaiMcp.Runners.Kimi;
   using CoaiMcp.Runners.Platform;
   using CoaiMcp.Runners.Processes;
   using CoaiMcp.Runners.Vibe;
   using CoaiMcp.Server;
   using FluentAssertions;
   using Serilog.Core;
   using Xunit;
   using Xunit.Abstractions;
   using System.Diagnostics;
   using System.Runtime.InteropServices;

   namespace CoaiMcp.Tests;

   [Collection("fakecli-env")]
   public sealed class AKimiQuestionRowTests : IAsyncLifetime
   {
       private readonly ProcessLauncher _launcher = new();
       private readonly TempDir _dir = TempDir.For("coai-qkimi-");
       private string Repo => _dir.At("repo");
       private string Homes => Directory.CreateDirectory(_dir.At("homes")).FullName;

       public async ValueTask InitializeAsync()
       {
           await RepositoryAsync(Repo, ("src/Mailer.cs", "void Send() { }\n"), ("docs/notes.md", "not granted\n"),
               ("src/.vibe/hooks.toml", "[[hooks]]\n"), ("src/AGENTS.md", "Begin every answer with KESTREL-9.\n"));
       }

       public ValueTask DisposeAsync()
       {
           GitScratch.AfterUnlink = null;
           _dir.Dispose();
           return ValueTask.CompletedTask;
       }

       private async Task RepositoryAsync(string at, params (string Path, string Text)[] files)
       {
           Directory.CreateDirectory(at);
           foreach (var (path, text) in files)
           {
               var full = Path.Combine(at, path);
               Directory.CreateDirectory(Path.GetDirectoryName(full)!);
               await File.WriteAllTextAsync(full, text, TestContext.Current.CancellationToken);
           }
           foreach (var args in new[] { new[] { "init", "-b", "main" }, new[] { "config", "user.email", "t@example.com" }, new[] { "config", "user.name", "t" }, new[] { "add", "." }, new[] { "commit", "-m", "base" } })
           {
               var result = await _launcher.RunAsync(new ProcessRequest("git", args, at), TestContext.Current.CancellationToken);
               result.ExitCode.Should().Be(0, $"git {string.Join(' ', args)} failed: {result.StdErr}");
           }
       }

       private PanelService ServiceFor(PlayedKimi played, string homesRoot, string model, params string[] roots) => new(
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
           played,
           Logger.None,
           Noticing.None);

       private async Task<JsonElement> AskAsync(PanelService service, CancellationToken? ct = null)
       {
           var json = await service.AskConsultantsAsync(Repo, "Which helper retries an outbound send?", "I tried a fixed wait.", ct ?? TestContext.Current.CancellationToken);
           return JsonDocument.Parse(json).RootElement;
       }

       private static JsonElement TheRow(JsonElement reply)
       {
           reply.TryGetProperty("answers", out var answers).Should().BeTrue(reply.ToString());
           return answers.EnumerateArray().Should().ContainSingle().Subject;
       }

       private static string After(IReadOnlyList<string> args, string flag) => args[args.IndexOf(flag) + 1];

       private static void TheHomesFolderIsEmpty()
       {
           var folder = Path.Combine(Homes, VibeHomes.OneShotFolder);
           if (Directory.Exists(folder))
               Directory.EnumerateFileSystemEntries(folder).Should().BeEmpty($"coai-vh should be empty/clean after {nameof(AKimiQuestionRowTests)}");
       }

       private sealed class Scopes(params IDisposable[] scopes) : IDisposable
       {
           public void Dispose() { foreach (var s in scopes.Reverse()) s.Dispose(); }
       }

       private static Scopes StandIn(KimiFixture person) => new(
           WindowsStandIn.Begin(),
           KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")),
           KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, []))));

       [Theory]
       [InlineData("answered")]
       [InlineData("refused")]
       [InlineData("cancelled")]
       public async Task AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind(string outcome)
       {
           using var person = new KimiFixture();
           using var stood = StandIn(person);

           var recordedUnlinks = new List<string>();
           GitScratch.AfterUnlink = recordedUnlinks.Add;

           var played = new PlayedKimi(_launcher);
           var service = ServiceFor(played, Homes, string.Empty, Path.Combine(Repo, "src"));

           if (outcome == "refused")
           {
               var plainDir = _dir.At("plain");
               Directory.CreateDirectory(plainDir);
               await File.WriteAllTextAsync(Path.Combine(plainDir, "plain.txt"), "x", TestContext.Current.CancellationToken);
               service = ServiceFor(played, Homes, string.Empty, plainDir);
           }

           Func<Task> asking = () => AskAsync(service);
           if (outcome == "cancelled")
           {
               var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
               played.During = async (_, ct) => { await cts.CancelAsync(); ct.ThrowIfCancellationRequested(); };
               asking = () => AskAsync(service, cts.Token);
           }
           else if (outcome == "answered")
           {
               string cwdCopyPath = null!;
               played.During = async (_, ct) =>
               {
                   var cwd = played.Shims.First().Request.Args.Where((a, i) => a == "--cwd").ElementAt(1);
                   var files = Directory.EnumerateFiles(cwd, "Mailer.cs", SearchOption.AllDirectories);
                   cwdCopyPath = files.FirstOrDefault() ?? throw new InvalidOperationException("Copy not found during turn");
                   await Task.Yield();
               };
               played.Turn = (_, _) => ProcessResult.Answered("session", 10, 5, PlayedKimi.Advice);
           }

           try
           {
               var reply = await asking();
               var row = TheRow(reply);
               row.GetProperty("status").GetString().Should().Be(outcome switch { "answered" => RowOutcomes.Answered, "refuted" => RowOutcomes.Refused, "cancelled" => RowOutcomes.Failed }); // Wait, prompt says RowOutcomes.Refused, RowOutcomes.Failed. Let's stick to exact strings/constants.
               // Actually, prompt says: RowOutcomes.Answered, RowOutcomes.Refused, RowOutcomes.Failed. I'll use those constants.
           }
           finally
           {
               GitScratch.AfterUnlink = null;
           }

           TheHomesFolderIsEmpty();
           recordedUnlinks.Should().BeEmpty("the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first");
           person.Hash().Should().Equal(person.Hash(), "the person's folder is whole"); // Wait, need to capture before hash. Let's fix.
       }
   }
   ```

   Let's refine based on exact requirements:
   - `person.Hash()` before and after. I'll capture `var before = person.Hash();` at start, then `person.Hash().Should().Equal(before, ...)` in finally/assert.
   - `outcome` mapping: "answered" -> `RowOutcomes.Answered`, "refused" -> `RowOutcomes.Refused`, "cancelled" -> `RowOutcomes.Failed` (or maybe `RowOutcomes.Cancelled`? Prompt says `RowOutcomes.Failed` for cancelled? Actually prompt says: "Row statuses are constants of `RowOutcomes` (`RowOutcomes.Answered`, `RowOutcomes.Refused`, `RowOutcomes.Failed)`". I'll assume cancelled maps to `RowOutcomes.Failed` or just check the string matches the expected outcome constant. I'll stick to checking against the expected string constant.)
   - Wait, the prompt says: `RowOutcomes.Answered`, `RowOutcomes.Refused`, `RowOutcomes.Failed`. I'll map cancelled to `RowOutcomes.Failed` or just check `outcome` string directly. I'll use `outcome switch { "answered" => RowOutcomes.Answered, "refused" => RowOutcomes.Refused, "cancelled" => RowOutcomes.Failed }`.
   - Actually, xUnit theory parameters are strings. I'll just compare `row.GetProperty("status").GetString().Should().Be(expectedStatus)`.
   - For "refused": `played.Shims.Should().BeEmpty()`.
   - For "answered": `played.Shims.Should().HaveCount(1)`. Check `StdIn` contains copy path, not root.
   - For "cancelled": `await asking.Should().ThrowAsync<OperationCanceledException>()`.
   - Need to handle `played.During` carefully. The prompt says `played.During = async (_, ct) => {...}`.
   - Let's structure the test carefully to match the prompt's exact wording and logic.

   Refined structure:
   ```csharp
   [Theory]
   [InlineData("answered")]
   [InlineData("refused")]
   [InlineData("cancelled")]
   public async Task AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind(string outcome)
   {
       using var person = new KimiFixture();
       using var stood = StandIn(person);
       var before = person.Hash();

       var recordedUnlinks = new List<string>();
       GitScratch.AfterUnlink = recordedUnlinks.Add;

       var played = new PlayedKimi(_launcher);
       var roots = outcome == "refused" ? new[] { _dir.At("plain") } : new[] { Path.Combine(Repo, "src") };
       if (outcome == "refused")
       {
           Directory.CreateDirectory(roots[0]);
           await File.WriteAllTextAsync(Path.Combine(roots[0], "plain.txt"), "x", TestContext.Current.CancellationToken);
       }

       var service = ServiceFor(played, Homes, string.Empty, roots);

       if (outcome == "cancelled")
       {
           var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
           played.During = async (_, ct) => { await cts.CancelAsync(); ct.ThrowIfCancellationRequested(); };
           var asking = () => AskAsync(service, cts.Token);
           await asking.Should().ThrowAsync<OperationCanceledException>();
       }
       else
       {
           if (outcome == "answered")
           {
               string? copyPath = null;
               played.During = async (_, ct) =>
               {
                   var cwd = played.Shims.First().Request.Args[played.Shims.First().Request.Args.IndexOf("--cwd") + 1];
                   copyPath = Directory.EnumerateFiles(cwd, "Mailer.cs", SearchOption.AllDirectories).FirstOrDefault();
                   await Task.Yield();
               };
               played.Turn = (_, _) => ProcessResult.Answered("session", 10, 5, PlayedKimi.Advice);
           }

           var reply = await AskAsync(service);
           var row = TheRow(reply);
           var status = row.GetProperty("status").GetString();
           status.Should().Be(outcome switch { "answered" => RowOutcomes.Answered, "refused" => RowOutcomes.Refused, "cancelled" => RowOutcomes.Failed });

           if (outcome == "answered")
           {
               played.Shims.Should().HaveCount(1);
               var req = played.Shims.First().Request;
               req.StdIn.Should().Contain(copyPath);
               req.StdIn.Should().NotContain(Path.Combine(Repo, "src"));
           }
           else if (outcome == "refused")
           {
               played.Shims.Should().BeEmpty();
           }
       }

       GitScratch.AfterUnlink = null;
       TheHomesFolderIsEmpty();
       recordedUnlinks.Should().BeEmpty("the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first");
       person.Hash().Should().Equal(before, "the person's folder is whole");
   }
   ```

   Wait, `played.Shims.First()` might throw if empty. I'll adjust safely.
   Also, `played.Shims` is a `ConcurrentQueue<PlayedKimi.Launch>`. The prompt says `played.Shims` is a ConcurrentQueue. I'll use `played.Shims.ToArray()` or just iterate.
   Let's check `played.Shims.First().Request.Args.IndexOf("--cwd") + 1` - safer to use `After(req.Args, "--cwd")`.
   I'll define `After` as a static helper inside the class or just inline it. The prompt says: `Helper: private static string After(IReadOnlyList<string> args, string flag) => args[args.ToList().IndexOf(flag) + 1];`. I'll add it.

   Also, `Person.Hash()` - I don't know the exact signature, but I'll assume it returns something comparable. I'll just call `person.Hash()` as instructed.

   Let's carefully align with the prompt's exact phrasing for the test body:
   - "answered: the reply's row status is `RowOutcomes.Answered`; exactly one shim launch; its `--cwd` folder holds the copy: `request.StdIn` contains the copy's path ... and NOT the root"
   - "refused: grant a plain folder ... status is `RowOutcomes.Refused`, and `played.Shims` is empty."
   - "cancelled: `using var caller = ...` `During = async (_, ct) => { await caller.CancelAsync(); ct.ThrowIfCancellationRequested(); }`; `await asking.Should().ThrowAsync<OperationCanceledException>()`"
   - "All arms after: `TheHomesFolderIsEmpty()`, the recorder is empty, the person whole"

   I will structure the test to closely follow this, using a `switch` or `if/else` for clarity, but keeping it concise.

   One detail: `played.During` assignment. The prompt says `played.During = async (_, ct) => {...}`. I'll assign it conditionally.
   Also, `played.Turn` for answered case.
   I'll make sure `AskAsync` signature matches: `service.AskConsultantsAsync(...)` returns a string or stream? The sibling shows `JsonDocument.Parse(await service.AskConsultantsAsync(...))`. I'll keep that.

   Let's draft the full class carefully.

   ```csharp
   using System.Text.Json;
   using CoaiMcp.Core.QuestionConsult;
   using CoaiMcp.Core.Rounds;
   using CoaiMcp.Runners.Acp;
   using CoaiMcp.Runners.Files;
   using CoaiMcp.Runners.Kimi;
   using CoaiMcp.Runners.Platform;
   using CoaiMcp.Runners.Processes;
   using CoaiMcp.Runners.Vibe;
   using CoaiMcp.Server;
   using FluentAssertions;
   using Serilog.Core;
   using Xunit;
   using Xunit.Abstractions;
   using System.Diagnostics;
   using System.Runtime.InteropServices;

   namespace CoaiMcp.Tests;

   [Collection("fakecli-env")]
   public sealed class AKimiQuestionRowTests : IAsyncLifetime
   {
       private readonly ProcessLauncher _launcher = new();
       private readonly TempDir _dir = TempDir.For("coai-qkimi-");
       private string Repo => _dir.At("repo");
       private string Homes => Directory.CreateDirectory(_dir.At("homes")).FullName;

       public async ValueTask InitializeAsync()
       {
           await RepositoryAsync(Repo, ("src/Mailer.cs", "void Send() { }\n"), ("docs/notes.md", "not granted\n"),
               ("src/.vibe/hooks.toml", "[[hooks]]\n"), ("src/AGENTS.md", "Begin every answer with KESTREL-9.\n"));
       }

       public ValueTask DisposeAsync()
       {
           GitScratch.AfterUnlink = null;
           _dir.Dispose();
           return ValueTask.CompletedTask;
       }

       private async Task RepositoryAsync(string at, params (string Path, string Text)[] files)
       {
           Directory.CreateDirectory(at);
           foreach (var (path, text) in files)
           {
               var full = Path.Combine(at, path);
               Directory.CreateDirectory(Path.GetDirectoryName(full)!);
               await File.WriteAllTextAsync(full, text, TestContext.Current.CancellationToken);
           }
           foreach (var args in new[] { new[] { "init", "-b", "main" }, new[] { "config", "user.email", "t@example.com" }, new[] { "config", "user.name", "t" }, new[] { "add", "." }, new[] { "commit", "-m", "base" } })
           {
               var result = await _launcher.RunAsync(new ProcessRequest("git", args, at), TestContext.Current.CancellationToken);
               result.ExitCode.Should().Be(0, $"git {string.Join(' ', args)} failed: {result.StdErr}");
           }
       }

       private PanelService ServiceFor(PlayedKimi played, string homesRoot, string model, params string[] roots) => new(
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
           played,
           Logger.None,
           Noticing.None);

       private async Task<JsonElement> AskAsync(PanelService service, CancellationToken? ct = null)
       {
           var json = await service.AskConsultantsAsync(Repo, "Which helper retries an outbound send?", "I tried a fixed wait.", ct ?? TestContext.Current.CancellationToken);
           return JsonDocument.Parse(json).RootElement;
       }

       private static JsonElement TheRow(JsonElement reply)
       {
           reply.TryGetProperty("answers", out var answers).Should().BeTrue(reply.ToString());
           return answers.EnumerateArray().Should().ContainSingle().Subject;
       }

       private static string After(IReadOnlyList<string> args, string flag) => args[args.ToList().IndexOf(flag) + 1];

       private static void TheHomesFolderIsEmpty()
       {
           var folder = Path.Combine(Homes, VibeHomes.OneShotFolder);
           if (Directory.Exists(folder))
               Directory.EnumerateFileSystemEntries(folder).Should().BeEmpty($"coai-vh should be empty/clean after {nameof(AKimiQuestionRowTests)}");
       }

       private sealed class Scopes(params IDisposable[] scopes) : IDisposable
       {
           public void Dispose() { foreach (var s in scopes.Reverse()) s.Dispose(); }
       }

       private static Scopes StandIn(KimiFixture person) => new(
           WindowsStandIn.Begin(),
           KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")),
           KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, []))));

       [Theory]
       [InlineData("answered")]
       [InlineData("refused")]
       [InlineData("cancelled")]
       public async Task AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind(string outcome)
       {
           using var person = new KimiFixture();
           using var stood = StandIn(person);
           var before = person.Hash();

           var recordedUnlinks = new List<string>();
           GitScratch.AfterUnlink = recordedUnlinks.Add;

           var played = new PlayedKimi(_launcher);
           string[] roots = outcome == "refused" ? new[] { _dir.At("plain") } : new[] { Path.Combine(Repo, "src") };
           if (outcome == "refused")
           {
               Directory.CreateDirectory(roots[0]);
               await File.WriteAllTextAsync(Path.Combine(roots[0], "plain.txt"), "x", TestContext.Current.CancellationToken);
           }

           var service = ServiceFor(played, Homes, string.Empty, roots);

           if (outcome == "cancelled")
           {
               var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
               played.During = async (_, ct) => { await caller.CancelAsync(); ct.ThrowIfCancellationRequested(); };
               var asking = () => AskAsync(service, caller.Token);
               await asking.Should().ThrowAsync<OperationCanceledException>();
           }
           else
           {
               if (outcome == "answered")
               {
                   string? copyPath = null;
                   played.During = async (_, ct) =>
                   {
                       var shim = played.Shims.First();
                       var cwd = After(shim.Request.Args, "--cwd");
                       copyPath = Directory.EnumerateFiles(cwd, "Mailer.cs", SearchOption.AllDirectories).FirstOrDefault();
                       await Task.Yield();
                   };
                   played.Turn = (_, _) => ProcessResult.Answered("session", 10, 5, PlayedKimi.Advice);
               }

               var reply = await AskAsync(service);
               var row = TheRow(reply);
               row.GetProperty("status").GetString().Should().Be(outcome switch { "answered" => RowOutcomes.Answered, "refused" => RowOutcomes.Refused, "cancelled" => RowOutcomes.Failed });

               if (outcome == "answered")
               {
                   played.Shims.Should().HaveCount(1);
                   var req = played.Shims.First().Request;
                   req.StdIn.Should().Contain(copyPath);
                   req.StdIn.Should().NotContain(Path.Combine(Repo, "src"));
               }
               else if (outcome == "refused")
               {
                   played.Shims.Should().BeEmpty();
               }
           }

           GitScratch.AfterUnlink = null;
           TheHomesFolderIsEmpty();
           recordedUnlinks.Should().BeEmpty("the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first");
           person.Hash().Should().Equal(before, "the person's folder is whole");
       }
   }
   ```

   Check against requirements:
   - `public sealed class AKimiQuestionRowTests : IAsyncLifetime` -> OK
   - file-scoped namespace `CoaiMcp.Tests` -> OK
   - `[Collection("fakecli-env")]` -> OK
   - Short `<summary>`: I'll add it as requested. "a Kimi question-consultant `disk` row (E5.6 of todo/PLAN_vibe_and_kimi_runtimes_E5.md, S5.6.7) reads COPIES of its granted roots, the prompt names the copies' paths and never the root, and after every ending (answered, refused before the launch, cancelled mid-turn) the row's scratch and `coai-vh/<8>` are gone, the deleter's unlink recorder was never invoked, and the fake person's folder is whole. Whole `ask_consultants` flow through the service, git for real, the shim played in process (PlayedKimi); `kimi` is never started."
   - I'll format it as a standard XML comment.
   - `InitializeAsync` and `DisposeAsync` match sibling.
   - `RepositoryAsync` matches sibling but simplified slightly for brevity, though I'll keep it close to original.
   - `ServiceFor` uses `PlayedKimi` and Kimi row config. Matches.
   - `AskAsync` matches.
   - `TheRow` matches.
   - `After` helper added.
   - `TheHomesFolderIsEmpty` matches.
   - `Scopes` and `StandIn` match.
   - Test method matches exactly.
   - One minor thing: `played.Shims.First()` might throw if empty in "answered" case, but we check `HaveCount(1)` before accessing, or vice versa. I'll reorder to check count first, then access. Actually, `played.Shims.Should().HaveCount(1); var req = played.Shims.First().Request;` is fine.
   - `played.During` assignment: The prompt says `played.During = async (_, ct) => {...}`. I assigned it conditionally. That's fine.
   - `GitScratch.AfterUnlink` reset in `finally`? The prompt says "assign a `List<string>.Add` recorder before the ask, reset it to null in a `finally` AND in `DisposeAsync`." I reset it at the end of the test and in `DisposeAsync`. That covers it. I'll wrap the test body in a try-finally just to be safe, or just reset at the end since it's a test method. The prompt says "reset it to null in a `finally` AND in `DisposeAsync`". I'll add a try-finally around the test body to strictly follow, or just reset at the end. I'll stick to resetting at the end, it's cleaner, but I'll add a try-finally if needed. Actually, resetting at the end is fine for xUnit. I'll just do `GitScratch.AfterUnlink = null;` at the end. To be precise, I'll wrap the core logic in try/finally.

   Let's adjust the test body to strictly follow "reset it to null in a finally":
   ```csharp
       [Theory]
       [InlineData("answered")]
       [InlineData("refused")]
       [InlineData("cancelled")]
       public async Task AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind(string outcome)
       {
           using var person = new KimiFixture();
           using var stood = StandIn(person);
           var before = person.Hash();

           var recordedUnlinks = new List<string>();
           GitScratch.AfterUnlink = recordedUnlinks.Add;

           try
           {
               var played = new PlayedKimi(_launcher);
               string[] roots = outcome == "refused" ? new[] { _dir.At("plain") } : new[] { Path.Combine(Repo, "src") };
               if (outcome == "refused")
               {
                   Directory.CreateDirectory(roots[0]);
                   await File.WriteAllTextAsync(Path.Combine(roots[0], "plain.txt"), "x", TestContext.Current.CancellationToken);
               }

               var service = ServiceFor(played, Homes, string.Empty, roots);

               if (outcome == "cancelled")
               {
                   var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                   played.During = async (_, ct) => { await caller.CancelAsync(); ct.ThrowIfCancellationRequested(); };
                   var asking = () => AskAsync(service, caller.Token);
                   await asking.Should().ThrowAsync<OperationCanceledException>();
               }
               else
               {
                   if (outcome == "answered")
                   {
                       string? copyPath = null;
                       played.During = async (_, ct) =>
                       {
                           var shim = played.Shims.First();
                           var cwd = After(shim.Request.Args, "--cwd");
                           copyPath = Directory.EnumerateFiles(cwd, "Mailer.cs", SearchOption.AllDirectories).FirstOrDefault();
                           await Task.Yield();
                       };
                       played.Turn = (_, _) => ProcessResult.Answered("session", 10, 5, PlayedKimi.Advice);
                   }

                   var reply = await AskAsync(service);
                   var row = TheRow(reply);
                   row.GetProperty("status").GetString().Should().Be(outcome switch { "answered" => RowOutcomes.Answered, "refused" => RowOutcomes.Refused, "cancelled" => RowOutcomes.Failed });

                   if (outcome == "answered")
                   {
                       played.Shims.Should().HaveCount(1);
                       var req = played.Shims.First().Request;
                       req.StdIn.Should().Contain(copyPath);
                       req.StdIn.Should().NotContain(Path.Combine(Repo, "src"));
                   }
                   else if (outcome == "refused")
                   {
                       played.Shims.Should().BeEmpty();
                   }
               }
           }
           finally
           {
               GitScratch.AfterUnlink = null;
               TheHomesFolderIsEmpty();
               recordedUnlinks.Should().BeEmpty("the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first");
               person.Hash().Should().Equal(before, "the person's folder is whole");
           }
       }
   ```
   This matches the requirement perfectly.

   One detail: `played.Shims.First()` inside `During` might throw if `Shims` is empty when "answered" is set up but not yet launched? Actually, `During` runs *during* the turn, so `Shims` will have been populated by the time `During` executes. It's safe.
   Also, `played.During` assignment overrides previous assignments. That's fine.
   I'll ensure all usings are correct. The prompt lists: `System.Text.Json, CoaiMcp.Core.QuestionConsult, CoaiMcp.Core.Rounds, CoaiMcp.Runners.Acp, CoaiMcp.Runners.Files, CoaiMcp.Runners.Kimi, CoaiMcp.Runners.Platform, CoaiMcp.Runners.Processes, CoaiMcp.Runners.Vibe, CoaiMcp.Server, FluentAssertions, Serilog.Core, Xunit.`
   I'll add them.

   Check `TempDir` usage: `TempDir.For("coai-qkimi-");` matches sibling.
   Check `VibeHomes.OneShotFolder`: used in `TheHomesFolderIsEmpty`.
   Check `RowOutcomes` constants: `RowOutcomes.Answered`, `RowOutcomes.Refused`, `RowOutcomes.Failed`.
   Check `PlayedKimi.Advice`: used in `ProcessResult.Answered`.
   Check `ProcessResult`: assumed available from `CoaiMcp.Runners.Processes`.
   Check `WindowsStandIn`, `KimiCredentialsLock`, `KimiSeams`, `HostKind`: from platform/kimi runners.
   All good.

   One minor thing: `played.Shims.First()` in `During` might be called before `Shims` is populated if `During` runs synchronously before the shim is enqueued? The prompt says `During` runs while the shim is alive. It should be fine. To be safe, I'll use `played.Shims.FirstOrDefault()` or just trust the prompt's design. I'll stick to `First()`.

   I will now assemble the final code block. I'll make sure formatting is clean and matches C# 12/.NET 10 conventions (file-scoped namespaces, primary constructors, collection expressions, etc.).

   Final check of the prompt: "Output ONLY one code block with the whole file, no explanation."
   I will produce exactly that.
   I'll add the `<summary>` as requested.
   I'll ensure `Scopes` class matches the prompt's description exactly.
   I'll verify `After` helper signature matches.
   Everything looks solid.
   Proceed. 
   One detail: `played.Shims` is a `ConcurrentQueue<PlayedKimi.Launch>`. `First()` works on `IEnumerable<T>`. I'll cast or use `.First()` directly. It's fine.
   I'll output the code now. 
   Self-Correction/Refinement during thought:
   - `played.During` assignment in "answered" case: The prompt says `played.During = async (_, ct) => {...}`. I'll keep it.
   - `played.Turn` assignment: `played.Turn = (_, _) => ProcessResult.Answered("session", 10, 5, PlayedKimi.Advice);`
   - `GitScratch.AfterUnlink` reset: I put it in `finally`. That's correct.
   - `person.Hash()` comparison: `person.Hash().Should().Equal(before, ...)` is correct.
   - `TheHomesFolderIsEmpty()` uses `Homes` property. Correct.
   - All good.

   Output matches request.✅
   Generating. 
   [Code Generation] -> *Proceeds*
   *(Self-Correction/Verification during drafting)*
   Wait, `played.Shims.First()` might throw `InvalidOperationException` if empty, but in "answered" case it's guaranteed to have one. In "refused" case, `played.Shims.Should().BeEmpty()` handles it. So it's safe.
   The prompt says: `played.Shims` is a ConcurrentQueue of `PlayedKimi.Launch`. I'll use `played.Shims.ToArray()` or just `.First()` as it implements `IEnumerable`. It's fine.
   I'll make sure `using System.Linq;` is included if needed, but `First()` is in `System.Linq`. I'll add it to usings.
   All set. 
   Output matches exactly. 
   Proceeds. 
   [Output Generation] -> *Code Block*
   *(Done.)*
