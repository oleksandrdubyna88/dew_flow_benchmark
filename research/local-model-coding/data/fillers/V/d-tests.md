Write a new xUnit test class for a C# repo. Style: C# 14 / .NET 10, xUnit v3 + FluentAssertions, file-scoped namespace `CoaiMcp.Tests`, 4-space indent, Allman braces, target-typed `new`, collection expressions, one short /// summary per test naming its scenario id. Output the complete class (without usings) in one ```csharp block.

Class name: `AKimiConsultantCheckTests`, `[Collection("fakecli-env")]`, `IDisposable`. It tests `ConsultantCheckMode.AnswerAsync`, the `--check-consultant` mode, with a Kimi consultant whose shim is played in-process by `PlayedKimi` (no real process).

Facts about helpers that already exist (all in namespace CoaiMcp.Tests unless noted):
- `PlayedKimi(IProcessLauncher inner)` : IProcessLauncher. Props: `ConcurrentQueue<Launch> Shims` (each `Launch` has `.Request` of type ProcessRequest with `.Arguments` (IReadOnlyList<string>), `.WorkingDirectory`), `Func<int, ProcessRequest, ProcessResult> Turn { get; init; }` (index of the shim launch, request -> the shim's result), `Func<ProcessRequest, CancellationToken, Task> During { get; init; }` (runs while the shim is "alive"). Statics: `PlayedKimi.Shim` (executable name string), `PlayedKimi.SessionOf(ProcessRequest)`, `PlayedKimi.Answered(string session, long tokensIn, long tokensOut, string answer = Advice)` -> ProcessResult, `PlayedKimi.Lost(string session)` -> ProcessResult (exit 70, end session_lost; use it as the "turn fails exit 70" result).
- `KimiFixture` : IDisposable — a fake person's Kimi home. `person.PlacesAt` is a `Func<string, KimiPlaces>`; `person.Hash()` returns IReadOnlyList<string> (the person's folder state); `person.Folder`.
- `WindowsStandIn.Begin()` : IDisposable. `KimiCredentialsLock.RootFor(string folder)` : IDisposable. `KimiSeams.Using(HostKind.Windows, new KimiSeams(person.PlacesAt, () => (PlayedKimi.Shim, [])))` : IDisposable (namespace CoaiMcp.Runners.Kimi; HostKind is CoaiMcp.Runners.Platform).
- `GitScratch.AfterUnlink` : static `Action<string>?` (namespace CoaiMcp.Runners.Files) invoked by the generic deleter whenever it unlinks a link; set it to a recorder `_unlinked.Add` and reset to null in Dispose. A Kimi home disposed junction-first never lets the deleter meet its junction, so the recorder must stay empty.
- `TempDir.For("testsToDelete-kimi-check-")` : IDisposable, implicit string conversion, `.Path`, `.At(name)`.
- `Noticing.None`, `ProcessLauncher` (real launcher, namespace CoaiMcp.Runners.Processes), `PanelSettings`, `ConsultantChoice`, `CallerIdentity.Claude`, `ConsultCheckStates` (consts Answered, Failed, Unavailable ...).
- Constants `Runners.Consultation.ConsultantHomes.Kimi`... NOT needed. The one-shot homes folder is named "coai-vh" (use the literal "coai-vh") under the homes root.

This existing helper in ConsultantCheckTests, copy it into your class (with fields `_data = Directory.CreateTempSubdirectory("coai-kcheck-data-").FullName`, `_temp = Directory.CreateTempSubdirectory("coai-kcheck-temp-").FullName`, `_warned = new List<string>()`; delete the two directories recursively in Dispose, swallowing IOException/UnauthorizedAccessException):
```csharp
private async Task<(int Code, JsonElement Said, string Err)> Check(PanelSettings settings, IProcessLauncher launcher, CancellationToken ct, params string[] args)
{
    var (code, stdout, stderr) = await ConsultantCheckMode.AnswerAsync(
        settings, ["--check-consultant", .. args], launcher, _temp, _warned.Add, Noticing.None, ct);

    return (code, stdout.Length > 0 ? JsonDocument.Parse(stdout).RootElement.Clone() : default, stderr);
}
private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? string.Empty;
```
Existing Vibe check test to imitate:
```csharp
using var homes = TempDir.For("testsToDelete-vh-check-");
var settings = Settings() with
{
    ConsultantHomesRoot = homes,   // the homes root; empty string means "rootless"
    Consultants = new Dictionary<string, ConsultantChoice>
    {
        [CallerIdentity.Claude] = new("mistral", Model: "glm-5-3", Runtime: "vibe", ExecutablePath: PlayedVibe.Exe),
    },
};
var (code, said, _) = await Check(settings, vibe, TestContext.Current.CancellationToken, "--caller", "claude");
code.Should().Be(0);
Text(said, "state").Should().Be("answered", said.ToString());
said.GetProperty("markerRead").GetBoolean().Should().BeTrue();
turn.Request.WorkingDirectory.Should().Contain(Path.Combine("own", "tree"));
Directory.EnumerateDirectories(_temp, "coai-check-*").Should().BeEmpty("the copy goes with the check's scratch");
```
The settings for a Kimi row: `new PanelSettings { Providers = [], DataDir = _data, ReviewerTimeout = TimeSpan.FromSeconds(60), ConsultEnabled = true, ConsultantHomesRoot = <root or string.Empty>, Consultants = new Dictionary<string, ConsultantChoice> { [CallerIdentity.Claude] = new("my-kimi", Runtime: "kimi") } }`. Write that as a private helper `Settings(string homesRoot)`.
To make the played shim answer the check, `Turn` must read the marker: the scratch copy holds a file `CHECK.md` in the shim's `--cwd` folder (the value after "--cwd" in request.Arguments), whose last word (trimmed of a trailing '.') is the marker; the answer text must be `$"marker: {marker}{Environment.NewLine}canary: CANNOT"`. So: `Turn = (_, request) => PlayedKimi.Answered(PlayedKimi.SessionOf(request), 100, 10, Answer(request))` with a private static `Answer(ProcessRequest)` doing that.
The shim's `--home` argument is `<root>/coai-vh/<8 chars>/kh-<10 hex>`; rootless it is `<scratch>/own/kh-<10 hex>`.
Each test starts with `using var person = new KimiFixture(); using var stood = StandIn(person);` where `StandIn(person)` is a private static helper returning a disposable bundle of the three scopes (WindowsStandIn.Begin(), KimiCredentialsLock.RootFor(Path.Combine(person.Folder, "locks")), KimiSeams.Using(...)); make a tiny private sealed class `Scopes(params IDisposable[] scopes) : IDisposable` that disposes them in reverse, and include it.

Write exactly these four tests, with these names:
1. `AKimiCheck_RunsInACopy_OnAOneShotHome` [Fact]: homes = TempDir.For("testsToDelete-vh-check-"); run the check with a played Kimi (new PlayedKimi(new ProcessLauncher()) with the Turn above); code 0; state "answered"; markerRead true; exactly one shim launch; its `--cwd` value contains Path.Combine("own", "tree"); its `--home` value starts with Path.Combine(homes, "coai-vh") + Path.DirectorySeparatorChar and its file name starts with "kh-" and has length 13; no `coai-check-*` directory left in _temp.
2. `AfterTheCheck_TheHomeIsGone_JunctionFirst` [Theory] with `[InlineData(false)]` (passed) and `[InlineData(true)]` (the turn fails with exit 70: Turn returns `PlayedKimi.Lost(PlayedKimi.SessionOf(request))`): record `GitScratch.AfterUnlink = _unlinked.Add` before, capture `var before = person.Hash()`; after the check: `Directory.EnumerateFileSystemEntries(Path.Combine(homes, "coai-vh"))` is empty or the folder is absent (use a helper `Directory.Exists(x) ? Directory.EnumerateFileSystemEntries(x) : []`), `_unlinked` is empty, `person.Hash()` equals `before`. For the failing arm also assert Text(said, "state") is not "answered".
3. `ACheckCancelledMidTurn_StillDisposesItsHome` [Fact]: `using var stop = new CancellationTokenSource();` the played shim has `During = async (_, ct) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }` where `started` is a `TaskCompletionSource`; start the check with stop.Token as a Task (not awaited yet), await `started.Task`, call `await stop.CancelAsync()`, then await the check task (it may either return a result or throw OperationCanceledException — wrap in try/catch of OperationCanceledException), then assert the `coai-vh` folder is empty or absent and `_unlinked` is empty, and the person's folder is unchanged.
4. `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` [Fact]: Settings with ConsultantHomesRoot = string.Empty; the played shim's `Turn` also records the `--home` value into a local string; afterwards: the recorded home contains Path.Combine("own", "kh-"); its parent folder no longer exists (Directory.Exists false for the home's parent: the scratch `own` folder is gone); no `coai-check-*` dir in _temp; `_unlinked` empty.
