using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>§3.1 and §3.2 as amended 2026-10-13 (second amendment of the day): a candidate whose P carries its OWN Claude settings — even
/// ones granting a directory outside the repository — is ADMITTED, because its tree drops those settings (<see cref="ScoutTrees"/>), and the
/// settings files never enter G: they configure the scouting tool, not the code under investigation.
/// <para>
/// The first amendment (PR #84) excluded such a candidate at admission. Rerun over the D10 pool it excluded 431 candidates — three whole
/// private repositories (50 of 50, 218 of 219, 161 of 161) and 2 of another's 50: every repository that commits its Claude settings with a
/// grant outside it. That is a selection bias against exactly the repositories that use the tool together, so the exclusion is replaced by
/// the removal.
/// </para></summary>
public sealed class ScoutProjectSettingsAdmissionTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    [Fact]
    public async Task A_P_whose_own_settings_grant_outside_the_repository_is_admitted_and_the_settings_never_enter_G()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await _repo.CommitManyAsync([("src/A.cs", "class A {}\n"), ("src/B.cs", "class B {}\n"), ("src/C.cs", "class C {}\n")], ("base", "2026-06-01T10:00:00+00:00"));
        var p = await _repo.CommitAsync((".claude/settings.json", """{ "permissions": { "additionalDirectories": [ "../.." ] } }"""), ("settings", "2026-06-02T10:00:00+00:00"));
        var fix = await _repo.CommitManyAsync(
            [
                ("src/A.cs", "class A { int x; }\n"), ("src/B.cs", "class B { int x; }\n"), ("src/C.cs", "class C { int x; }\n"),
                (".claude/settings.json", """{ "permissions": { "additionalDirectories": [ "../../shared" ] } }"""),
            ],
            ("fix", "2026-06-02T11:00:00+00:00"));

        var facts = (await History().FactsAsync(Candidate("grant-outside", p, fix), Ct)).Ok();
        var selection = ScoutCandidates.Admit([facts], new DateOnly(2026, 1, 1));

        selection.Admitted.Select(c => c.Candidate.Id.Value).Should().Equal(["grant-outside"], "its tree drops the settings, so their grant reaches nothing");
        facts.GroundTruth.Should().Equal(["src/A.cs", "src/B.cs", "src/C.cs"], "the tool's settings are not code a scout is asked to find");
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
