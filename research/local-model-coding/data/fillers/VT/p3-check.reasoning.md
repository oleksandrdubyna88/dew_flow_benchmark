Here's a thinking process:

1.  **Understand the Goal**: The user wants a single C# file containing an xUnit v3 test class named `AKimiConsultantCheckTests` for .NET 10, using FluentAssertions. It should follow specific constraints, use provided facts, and implement four tests exactly as described.

2.  **Analyze Constraints & Requirements**:
   - **Language/Framework**: C#, .NET 10, xUnit v3, FluentAssertions.
   - **Namespace**: File-scoped `CoaiMcp.Tests`.
   - **Class**: `public sealed class AKimiConsultantCheckTests : IDisposable`.
   - **Attribute**: `[Collection("fakecli-env")]`.
   - **Doc Comment**: Short `<summary>` matching the prompt.
   - **Usings**: System.Text.Json, CoaiMcp.Runners.Acp, CoaiMcp.Runners.Files, CoaiMcp.Runners.Kimi, CoaiMcp.Runners.Platform, CoaiMcp.Runners.Processes, CoaiMcp.Runners.Vibe, CoaiMcp.Server, FluentAssertions, Xunit.
   - **Helpers/Setup**: Copy shape from `ConsultantCheckTests` but adapted for Kimi.
   - **Facts to rely on**: Provided in the prompt. I need to implement them correctly.
   - **Tests**: Four specific tests with exact names and assertions.
   - **Helpers to create**: `KimiSettings`, `Played(...)`, `After(...)`, `Scopes` (already provided, but I'll include it if needed, though the prompt says "copy exactly" for Scopes, I'll just inline or keep it as is). Actually, the prompt says "copy exactly; Scopes is a tiny IDisposable...", so I'll include it.
   - **Cleanup**: Class `Dispose()` removes `_data`, `_temp`, resets `GitScratch.AfterUnlink`. Create `_person = new KimiFixture()` as a field.
   - **Output**: ONLY one code block with the whole file, no explanation.

3.  **Deconstruct the Tests & Setup**:
   - **Fields**: `_data`, `_temp`, `_warned`, `_person`.
   - **Settings Helper**: `Settings(string runtime = "codex", TimeSpan? timeout = null, bool enabled = true)` -> needs to be adapted for Kimi. The prompt shows `Settings()` creates providers, data dir, etc. I'll adapt it to work for Kimi checks. Actually, the prompt gives a specific `Settings` method in the sibling test. I'll replicate it but ensure it fits Kimi.
   - **Check Helper**: `Check(PanelSettings settings, IProcessLauncher launcher, CancellationToken ct, params string[] args)` -> calls `ConsultantCheckMode.AnswerAsync`.
   - **Text Helper**: `Text(JsonElement element, string name)`
   - **After Helper**: `After(IReadOnlyList<string> args, string flag)`
   - **Scopes**: Provided.
   - **PlayedKimi Setup**: Need to create a `PlayedKimi` instance with a `Turn` function that reads `CHECK.md` from the `--cwd` and returns an answered result with the marker. Also handle `During` for cancellation tests.
   - **Test 1**: `AKimiCheck_RunsInACopy_OnAOneShotHome`
     - Setup homes, settings, person, standin.
     - Run check.
     - Assert code 0, state "answered", markerRead true.
     - Assert shim launch count == 1.
     - Assert cwd contains "own/tree".
     - Assert home path structure matches `coai-vh/<8>/<kh-10hex>`.
     - Assert no `coai-check-*` in `_temp`.
   - **Test 2**: `AfterTheCheck_TheHomeIsGone_JunctionFirst` (Theory)
     - InlineData: false (success), true (fail with exit 70).
     - Record `GitScratch.AfterUnlink`.
     - Capture `person.Hash()`.
     - Run check.
     - Assert homes root empty.
     - Assert recorder empty.
     - Assert person hash unchanged.
     - For fail arm, skip state assertions.
   - **Test 3**: `ACheckCancelledMidTurn_StillDisposesItsHome`
     - Linked token source.
     - Shim `During` cancels token.
     - Run check, catch `OperationCanceledException`.
     - Assert homes root empty, recorder empty, person hash unchanged.
     - If caught exception, assert `said` not null-ish (if available).
   - **Test 4**: `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst`
     - Settings without `ConsultantHomesRoot`.
     - Run check.
     - Assert home path structure matches `own/kh-10hex`.
     - Assert `_temp` has no `coai-check-*`.
     - Assert recorder empty, person hash unchanged.

4.  **Draft - Step by Step Construction**:

   *Usings*:
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
   ```

   *Class & Fields*:
   ```csharp
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
   ```

   *Helpers*:
   ```csharp
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
   ```

   *PlayedKimi Setup Helper*:
   Need a helper to create a `PlayedKimi` with the correct `Turn` logic.
   ```csharp
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
   ```

   *Test 1*:
   ```csharp
       [Fact]
       public async Task AKimiCheck_RunsInACopy_OnAOneShotHome()
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
           var home = After(req.Arguments, "--home");
           Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(home))).Should().Be("coai-vh");
           Path.GetFileName(Path.GetDirectoryName(home)).Length.Should().Be(8);
           Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
           home.StartsWith(homes).Should().BeTrue();

           Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
       }
   ```

   *Test 2*:
   ```csharp
       [Theory]
       [InlineData(false)]
       [InlineData(true)]
       public async Task AfterTheCheck_TheHomeIsGone_JunctionFirst(bool failTurn)
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
   ```

   *Test 3*:
   ```csharp
       [Fact]
       public async Task ACheckCancelledMidTurn_StillDisposesItsHome()
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
           JsonElement? said = null;
           try
           {
               var played = Played(during: (_, ct) => { caller.CancelAsync(); ct.ThrowIfCancellationRequested(); });
               var (code, s, _) = await Check(settings, played, caller.Token, "--caller", "claude");
               said = s;
           }
           catch (OperationCanceledException) { }

           Directory.EnumerateFileSystemEntries(Path.Combine(homes, VibeHomes.OneShotFolder)).Should().BeEmpty();
           GitScratch.AfterUnlink.Should().BeNull(); // Wait, the prompt says "recorded list is empty", but I didn't set it up here. I'll set it up to be safe, or just check it's null/empty. The prompt says "the recorder is empty". I'll initialize it.
           // Actually, the prompt says: "assert (either way) that coai-vh holds no entries, the recorder is empty, and the person's folder is whole."
           // I'll add a recorder list for consistency.
       }
   ```
   Let's refine Test 3 to match exactly:
   ```csharp
       [Fact]
       public async Task ACheckCancelledMidTurn_StillDisposesItsHome()
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
           person.Hash().Should().Equal(new KimiFixture().Hash()); // Wait, person.Hash() should equal itself before? The prompt says "person's folder is whole", which implies unchanged. I'll capture initial hash like other tests.
       }
   ```
   Actually, the prompt says: "the recorder is empty, and the person's folder is whole." I'll capture initial hash and compare.
   Let's adjust Test 3 carefully:
   ```csharp
       [Fact]
       public async Task ACheckCancelledMidTurn_StillDisposesItsHome()
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
   ```
   Wait, `person.Hash()` returns `IReadOnlyList<string>`. Comparing with `Equal(initialHash)` is fine.

   *Test 4*:
   ```csharp
       [Fact]
       public async Task ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst()
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
           var home = After(played.Shims.First().Arguments, "--home");
           Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
           Path.GetFileName(Path.GetDirectoryName(home)).Should().Be("own");

           Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty();
           recorded.Should().BeEmpty();
           person.Hash().Should().Equal(initialHash);
       }
   ```

5.  **Review & Refine**:
   - Check usings: All required ones are included.
   - Check class signature: `public sealed class AKimiConsultantCheckTests : IDisposable`, `[Collection("fakecli-env")]`, file-scoped namespace.
   - Check summary: Matches prompt.
   - Check helpers: `Settings`, `Check`, `Text`, `After`, `Scopes`, `StandIn`, `Played`.
   - Check test names: Exactly as requested.
   - Check assertions: Match prompt closely.
   - Check disposal: Class `Dispose()` handles `_data`, `_temp`, `GitScratch.AfterUnlink = null`, `_person.Dispose()`. Wait, the prompt says "create `_person = new KimiFixture()` as a field and dispose it." I used `using var person = new KimiFixture();` inside tests. I should make `_person` a field as requested, or just follow the prompt literally: "create `_person = new KimiFixture()` as a field and dispose it." I'll stick to field `_person` and use it, or just keep `using var person` in tests if it's easier, but the prompt explicitly says "create `_person = new KimiFixture()` as a field and dispose it." I'll adjust to use `_person` as a field, but note that `KimiFixture` might need to be recreated per test or reused. The prompt says "create `_person = new KimiFixture()` as a field and dispose it." I'll just make it a field and instantiate it in constructor or property init, and dispose in `Dispose()`. Actually, it's safer to just use `using var person = new KimiFixture();` inside each test as I did, but I'll follow the prompt strictly: I'll add `private KimiFixture _person = new();` and use it, or just keep `using var person` and ignore the field instruction if it conflicts with test isolation. The prompt says "create `_person = new KimiFixture()` as a field and dispose it." I'll add it as a field and use it in tests, but reset it if needed. Actually, it's fine to just have it as a field and instantiate once, or just follow the exact wording. I'll stick to `using var person = new KimiFixture();` inside tests for isolation, but I'll also add the field `_person` if needed. Wait, the prompt says: "create `_person = new KimiFixture()` as a field and dispose it." I'll just add `private KimiFixture _person = new();` and use it, but since tests run in parallel or sequentially, reusing a fixture might cause issues. I'll just follow the prompt literally: I'll declare `private KimiFixture _person = new();` and use it in tests. To be safe, I'll just use `using var person = new KimiFixture();` in tests as it's standard, but I'll add the field `_person` as requested. Actually, I'll just make `_person` a field and instantiate it in the constructor or property initializer, and dispose in `Dispose()`. It's fine.

   Let's re-read carefully: "create `_person = new KimiFixture()` as a field and dispose it." I'll just add `private KimiFixture _person = new();` and use it. But to avoid cross-test pollution, I'll just use `using var person = new KimiFixture();` in each test and ignore the field instruction if it's contradictory, or I'll just follow it exactly. I'll stick to `using var person` for safety, but I'll add `_person` as a field anyway to satisfy the prompt. Actually, I'll just use `_person` as a field and reassign it per test if needed, or just trust it's fine. I'll keep it simple: `private KimiFixture _person = new();` and use it.

   Wait, the prompt says: "create `_person = new KimiFixture()` as a field and dispose it." I'll just add it.

   Let's check `GitScratch.AfterUnlink` reset: "ALWAYS reset it to null in `Dispose()` and in a `finally`." I'll add `finally { GitScratch.AfterUnlink = null; }` around test bodies or just rely on class `Dispose()`. The prompt says "ALWAYS reset it to null in `Dispose()` and in a `finally`." I'll wrap test bodies in try/finally or just reset in `Dispose()`. I'll add `finally { GitScratch.AfterUnlink = null; }` inside each test to be safe, or just rely on class `Dispose()`. I'll stick to class `Dispose()` as requested, but add a `finally` block in tests if needed. Actually, it's easier to just reset in `Dispose()` and maybe in a `finally` of a helper. I'll just reset in `Dispose()` as instructed.

   Let's verify `PlayedKimi` setup: `PlayedKimi.Answered` takes `(string session, long tokensIn, long tokensOut, string answer)`. The prompt says: `PlayedKimi.Answered(PlayedKimi.SessionOf(request), 20000, 30, thatAnswer)`. Correct.
   `PlayedKimi.Lost(string session)` returns exit 70.
   `PlayedKimi.Minted` is session id.
   `PlayedKimi.SessionOf(request)`.
   All good.

   One detail: `FakeCliExe` is referenced in `Settings`. It's likely a static property from the test environment. I'll assume it exists as `FakeCliExe`.
   `TempDir.For` is used. I'll assume it's available.
   `WindowsStandIn`, `KimiCredentialsLock`, `KimiSeams`, `HostKind`, `CallerIdentity`, `ConsultantChoice`, `PanelSettings`, `ConsultantCheckMode`, `Noticing`, `VibeHomes`, `GitScratch`, `ProcessRequest`, `ProcessResult`, `TimedOut`, `AcpWords`, `JsonElement`, `JsonDocument`, `FluentAssertions`, `Xunit`, `System.Text.Json`, `CoaiMcp.*` are all assumed available.

   I will structure the code carefully.

   One thing: `Scopes` is provided. I'll include it exactly.
   `After` helper: `private static string After(IReadOnlyList<string> args, string flag) => args[args.ToList().IndexOf(flag) + 1];`
   `Settings` helper: I'll adapt it to match the prompt's shape but ensure it compiles conceptually.
   `Check` helper: matches prompt.
   `Text` helper: matches prompt.

   Let's assemble carefully.

   Check test 2: `AfterTheCheck_TheHomeIsGone_JunctionFirst` with `[InlineData(false)]` and `[InlineData(true)]`. The prompt says: "For the failing arm do not assert the state of the check, only the cleanup." I handled that.
   Check test 3: "If it returned, assert nothing about `said` other than that it is not null-ish (do not assert)." I'll adjust: `if (said.HasValue) said.Should().NotBeNull();` is fine.
   Check test 4: "settings with NO `ConsultantHomesRoot` (leave the default empty)". I'll omit `ConsultantHomesRoot` in settings.

   One minor point: `GitScratch.AfterUnlink` is a static property. I need to make sure I don't leak it between tests. I'll reset it in `finally` blocks or just rely on class `Dispose()`. The prompt says "ALWAYS reset it to null in `Dispose()` and in a `finally`." I'll add `finally { GitScratch.AfterUnlink = null; }` to each test or a helper. I'll just put it in a `finally` inside each test to be strictly compliant.

   Let's draft the full code.

   Structure:
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
           var home = After(req.Arguments, "--home");
           Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(home))).Should().Be("coai-vh");
           Path.GetFileName(Path.GetDirectoryName(home)).Length.Should().Be(8);
           Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
           home.StartsWith(homes).Should().BeTrue();

           Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
           finally { GitScratch.AfterUnlink = null; }
       }
       // ... wait, `finally` can't be used like that outside a try block. I'll wrap test bodies in try/finally or just rely on class Dispose. The prompt says "ALWAYS reset it to null in `Dispose()` and in a `finally`." I'll add a try/finally around each test body or just reset in Dispose. I'll stick to resetting in Dispose, but to be strictly compliant, I'll wrap each test in try/finally. Actually, it's simpler to just reset in Dispose and assume tests don't crash unpredictably. I'll add `finally { GitScratch.AfterUnlink = null; }` inside a try block if needed, but it's fine. I'll just reset in Dispose as instructed.
   ```

   Let's fix the `finally` requirement properly. I'll wrap each test in `try { ... } finally { GitScratch.AfterUnlink = null; }`. Or better, just reset in `Dispose()` as the prompt emphasizes class `Dispose()`. I'll stick to class `Dispose()` but add `finally` blocks where appropriate. Actually, the prompt says "ALWAYS reset it to null in `Dispose()` and in a `finally`." I'll interpret this as resetting in `Dispose()` and also in a `finally` block within tests to guarantee cleanup even on exceptions. I'll add `finally { GitScratch.AfterUnlink = null; }` to each test.

   Refined Test 1:
   ```csharp
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
               var home = After(req.Arguments, "--home");
               Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(home))).Should().Be("coai-vh");
               Path.GetFileName(Path.GetDirectoryName(home)).Length.Should().Be(8);
               Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
               home.StartsWith(homes).Should().BeTrue();

               Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
           }
           finally { GitScratch.AfterUnlink = null; }
       }
   ```
   This is verbose but safe. I'll apply similar structure to others, or just rely on `Dispose()` as it's cleaner. The prompt says "ALWAYS reset it to null in `Dispose()` and in a `finally`." I'll just add `finally { GitScratch.AfterUnlink = null; }` to each test.

   Actually, I can simplify by creating a helper method that wraps the test logic, but it's fine as is.

   Let's check Test 2:
   ```csharp
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
   ```

   Test 3:
   ```csharp
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
   ```

   Test 4:
   ```csharp
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
               var home = After(played.Shims.First().Arguments, "--home");
               Path.GetFileName(home).Should().MatchRegex(@"^kh-[0-9a-f]{10}$");
               Path.GetFileName(Path.GetDirectoryName(home)).Should().Be("own");

               Directory.EnumerateFileSystemEntries(_temp, "coai-check-*").Should().BeEmpty();
               recorded.Should().BeEmpty();
               person.Hash().Should().Equal(initialHash);
           }
           finally { GitScratch.AfterUnlink = null; }
       }
   ```

   One detail: `GitScratch.AfterUnlink` is static. Setting it to `null` in `finally` is good.
   The prompt says: "create `_person = new KimiFixture()` as a field and dispose it." I'll add `private KimiFixture _person = new();` and use it instead of `using var person` to strictly follow, but `using var person` is safer for isolation. I'll stick to `using var person` but also declare `_person` as a field if needed. Actually, I'll just use `_person` as a field and reassign it per test, or just ignore the field instruction if it causes issues. I'll keep `using var person` for safety, but I'll add `private KimiFixture _person = new();` to satisfy the prompt literally. I'll just use `_person` in tests to be consistent. Wait, reusing a fixture across tests might cause state leakage. I'll stick to `using var person = new KimiFixture();` inside tests, it's standard. The prompt says "create `_person = new KimiFixture()` as a field and dispose it." I'll just add it as a field and use it, but I'll reinitialize it if needed. Actually, it's fine.

   Let's check `FakeCliExe`. It's likely a static property in the test assembly. I'll assume it's available.
   `TempDir.For` is used.
   `VibeHomes.OneShotFolder` is used.
   `AcpWords.HomeFlag` is mentioned but I used `"--home"` directly. The prompt says: "use `AcpWords.HomeFlag`". I'll change `"--home"` to `AcpWords.HomeFlag`.
   Let's update `After` usage and `PlayedKimi` setup to use `AcpWords.HomeFlag`.
   Actually, `After` looks for the flag in arguments. If I use `AcpWords.HomeFlag`, it's safer.
   I'll replace `"--home"` with `AcpWords.HomeFlag` in `After` calls.

   Also, `PlayedKimi.Answered` signature: `PlayedKimi.Answered(PlayedKimi.SessionOf(request), 20000, 30, thatAnswer)`. Correct.
   `ProcessResult` constructor: `new ProcessResult(70, string.Empty, "the agent failed", TimedOut: false)`. Correct.
   `JsonElement.Clone()` is valid in .NET 8+.
   `FluentAssertions` methods: `.Should().Be()`, `.Should().BeEmpty()`, `.Should().MatchRegex()`, `.Should().Contain()`, `.Should().HaveCount()`, `.Should().NotBeNull()`, `.Should().Equal()`. All valid.

   One thing: `GitScratch.AfterUnlink` is `Action<string>?`. I assign `recorded.Add`. That works.
   `person.Hash()` returns `IReadOnlyList<string>`. Comparing with `Equal(initialHash)` works.
   `Directory.EnumerateFileSystemEntries` returns `IEnumerable<string>`. `.Should().BeEmpty()` works.
   `played.Shims` is `ConcurrentQueue<PlayedKimi.Launch>`. `.Should().HaveCount(1)` works.
   `played.Shims.First()` works.

   I'll adjust `"--home"` to `AcpWords.HomeFlag` as requested.
   `After(req.Arguments, AcpWords.HomeFlag)`
   `After(request.Arguments, "--cwd")` -> prompt says `--cwd <dir>`, so keep it.
   `After(request.Arguments, AcpWords.HomeFlag)`

   Let's verify `AcpWords.HomeFlag` constant. I'll just use `AcpWords.HomeFlag`.

   Final check of the code structure:
   - Usings at top.
   - Namespace.
   - Summary.
   - Collection attribute.
   - Class declaration.
   - Fields.
   - Dispose.
   - Helpers.
   - Tests.
   - All constraints met.

   I will produce the final code block now.
   One minor adjustment: `GitScratch.AfterUnlink = null;` in `finally` blocks is good. I'll keep it.
   The prompt says "create `_person = new KimiFixture()` as a field and dispose it." I'll add `private KimiFixture _person = new();` and use it in tests to be strictly compliant, but I'll reassign it per test to avoid state issues, or just use `using var person` and ignore the field part if it's contradictory. I'll stick to `using var person` for correctness, but I'll add `_person` as a field anyway. Actually, I'll just use `_person` as a field and instantiate it once, it's fine for tests. I'll change `using var person` to `_person = new KimiFixture();` inside tests.

   Wait, `using var person` is safer. I'll keep it. The prompt's instruction might just be suggesting how to structure it. I'll follow the spirit.

   Let's generate. 
   Self-Correction/Refinement during thought:
   - `AcpWords.HomeFlag` should be used.
   - `GitScratch.AfterUnlink` reset in `finally` is done.
   - `PlayedKimi` setup matches exactly.
   - All test names match exactly.
   - Assertions match exactly.
   - Code is self-contained.

   I will output the code block now. 
   One detail: `played.Shims.First()` might throw if empty, but we assert count 1 first. It's fine.
   `JsonElement.Clone()` is correct.
   `FluentAssertions` syntax is correct.
   `xUnit v3` uses `[Fact]`, `[Theory]`, `[InlineData]`, `TestContext.Current.CancellationToken`. All correct.
   Ready. 
   Output matches response. 
   Proceeds. 
   [Code Generation] -> *outputs the code block*
   *(Self-Correction/Verification during drafting)*
   - Check `AcpWords.HomeFlag`: I'll use `AcpWords.HomeFlag` in `After` calls.
   - Check `GitScratch.AfterUnlink`: It's static. Resetting in `finally` is correct.
   - Check `Scopes`: Included exactly.
   - Check `StandIn`: Included exactly.
   - Check `Settings`: Matches prompt.
   - Check `Check`: Matches prompt.
   - Check `Text`: Matches prompt.
   - Check `After`: Matches prompt.
   - Check `Played`: Matches prompt logic.
   - Tests: All 4 implemented exactly.
   - Disposal: Class `Dispose()` handles `_data`, `_temp`, `GitScratch.AfterUnlink`, `_person`.
   - All good.
   Output matches. 
   Proceeds. 
   [Final Code Generation] -> *outputs*
