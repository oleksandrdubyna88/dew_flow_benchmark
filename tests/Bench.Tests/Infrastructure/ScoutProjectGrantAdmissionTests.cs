using System.Text.Json;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>§3.1 as amended 2026-10-13: a candidate whose P carries its OWN Claude settings granting a directory outside the repository is
/// EXCLUDED at admission, read off the bare mirror — not refused when its tree is built. Found live: the first selection over the D10 pool drew
/// a private task whose P granted a directory outside the tree, and the tree guard (<see cref="ScoutGrantGuard.ProjectRefusal"/>) stopped the
/// WHOLE selection (exit 3) although its own words say "the task is excluded". Excluding it at admission keeps the draw mechanical: the
/// repository's next candidate takes its place by the same seeded order. The reason is a CODE — never the granted path, which for a private
/// repository names the operator's own folders.</summary>
public sealed class ScoutProjectGrantAdmissionTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    [Fact]
    public async Task A_P_whose_own_settings_grant_outside_the_repository_is_excluded_at_admission_with_a_code_and_an_inside_grant_is_admitted()
    {
        var elsewhere = _root.Sibling("elsewhere");
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await _repo.CommitManyAsync([("src/A.cs", "class A {}\n"), ("src/B.cs", "class B {}\n"), ("src/C.cs", "class C {}\n")], ("base", "2026-06-01T10:00:00+00:00"));

        var outside = await LandAsync(Settings("""[ "../.." ]"""), 1);
        var inside = await LandAsync(Settings("""[ "./src" ]"""), 2);
        var local = await LandLocalAsync(Settings($"[ {JsonSerializer.Serialize(elsewhere)} ]"), 3);
        var unreadable = await LandAsync("{ \"permissions\": { \"additionalDirectories\": [ \"./src\" ", 4);

        var facts = new List<ScoutCandidateFacts>();
        foreach (var (id, landed) in new[] { ("grant-outside", outside), ("grant-inside", inside), ("grant-local-outside", local), ("grant-unreadable", unreadable) })
        {
            facts.Add((await History().FactsAsync(Candidate(id, landed.P, landed.Fix), Ct)).Ok());
        }

        var selection = ScoutCandidates.Admit(facts, new DateOnly(2026, 1, 1));

        selection.Admitted.Select(c => c.Candidate.Id.Value).Should().Equal(["grant-inside"], "a grant inside the tree reaches nothing the scout may not read");
        selection.Excluded.Select(e => (e.Id.Value, e.Reason)).Should().Equal(
            ("grant-outside", ScoutCandidates.ProjectGrantsOutside),
            ("grant-local-outside", ScoutCandidates.ProjectGrantsOutside),
            ("grant-unreadable", ScoutCandidates.ProjectSettingsUnreadable));
        selection.Excluded.Should().OnlyContain(e => !e.Reason.Contains(elsewhere) && !e.Reason.Contains(".."), "a reason code never carries the granted path");
    }

    private static string Settings(string directories) => $$"""{ "permissions": { "additionalDirectories": {{directories}} } }""";

    private Task<(string P, string Fix)> LandAsync(string settingsJson, int round) => LandAsync([(".claude/settings.json", settingsJson)], round);

    /// <summary>A <c>settings.local.json</c> is usually git-ignored (a global excludes file on this machine ignores it), but a repository can
    /// still commit one — and the CLI reads it. Added with <c>-f</c>, beside an inside-only <c>settings.json</c>.</summary>
    private async Task<(string P, string Fix)> LandLocalAsync(string localJson, int round)
    {
        var file = Path.Combine(_repo.Root, ".claude", "settings.local.json");
        await File.WriteAllTextAsync(file, localJson, Ct);
        await _repo.GitAsync("add", "-f", ".claude/settings.local.json");

        return await LandAsync([(".claude/settings.json", Settings("""[ "./src", "./src/." ]"""))], round);
    }

    /// <summary>P commits the settings, the fix right on top of it changes three source files — a local squash: the fix's one parent is P.</summary>
    private async Task<(string P, string Fix)> LandAsync(IReadOnlyList<(string Path, string Content)> settings, int round)
    {
        var p = await _repo.CommitManyAsync(settings, ($"settings {round}", $"2026-06-0{round + 1}T10:00:00+00:00"));
        var fix = await _repo.CommitManyAsync(
            [("src/A.cs", $"class A {{ int x{round}; }}\n"), ("src/B.cs", $"class B {{ int x{round}; }}\n"), ("src/C.cs", $"class C {{ int x{round}; }}\n")],
            ($"fix {round}", $"2026-06-0{round + 1}T11:00:00+00:00"));

        return (p, fix);
    }

    private ScoutHistory History() => new(
        new GitCheckoutProvider(CheckoutCacheOptions.Under(_root.Sibling("mirrors")), NullLogger<GitCheckoutProvider>.Instance),
        new ScoutTreeSettings(_root.Sibling("trees"), ScoutTreeSettings.GitHub, TimeSpan.FromMinutes(2)));

    private ScoutCandidate Candidate(string id, string parent, string fix) =>
        new(ScoutTaskId.Parse(id).Ok(), _repo.Url, CommitSha.Parse(parent).Ok(), CommitSha.Parse(fix).Ok(), ScoutQuestionKind.HowEndToEnd, "How does it work end to end?", string.Empty)
        {
            MergeStyle = ScoutMergeStyles.Parse("local-squash").Ok(),
        };

    public void Dispose()
    {
        _repo.Dispose();
        _root.Dispose();
    }
}
