using Bench.Domain.Gate;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The encoding <c>scout-commit-v1</c> (plan §4 S4, §5 E2 "Private tasks"): a distinct label, field or content gives a distinct MAC; a
/// separator inside a value cannot make two inputs read as one (the fields are length-prefixed); G commits the same whatever order its paths
/// are listed in; a statement commits the same with CRLF or LF; the same key read twice (a restart) reproduces every commitment; and a wrong
/// key fails the verification BY LABEL, naming nothing the task holds.</summary>
public sealed class ScoutCommitmentTests
{
    private const string Campaign = "scouts-preliminary-2026-10-12";

    private static readonly FileHashKey Key = GateStoreFixtures.Key;

    private static readonly FileHashKey OtherKey = FileHashKey.Of(Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray()).Ok();

    [Fact]
    public void A_different_label_field_or_content_gives_a_different_commitment_and_the_same_inputs_the_same()
    {
        var one = ScoutCommitment.Of(Key, Campaign, "p03", ScoutCommitmentField.Statement, "x");

        ScoutCommitment.Of(Key, Campaign, "p03", ScoutCommitmentField.Statement, "x").Should().Be(one).And.HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        ScoutCommitment.Of(Key, Campaign, "p04", ScoutCommitmentField.Statement, "x").Should().NotBe(one, "another label");
        ScoutCommitment.Of(Key, Campaign, "p03", ScoutCommitmentField.Claim, "x").Should().NotBe(one, "another field");
        ScoutCommitment.Of(Key, Campaign, "p03", ScoutCommitmentField.Statement, "y").Should().NotBe(one, "another content");
        ScoutCommitment.Of(Key, "another-campaign", "p03", ScoutCommitmentField.Statement, "x").Should().NotBe(one, "another campaign");
        ScoutCommitment.Of(OtherKey, Campaign, "p03", ScoutCommitmentField.Statement, "x").Should().NotBe(one, "another key");
    }

    /// <summary>Under a naive join (<c>label · content</c>) these two would spell the same bytes; length-prefixing keeps them apart.</summary>
    [Theory]
    [InlineData("p03", "4x", "p034", "x")]
    [InlineData("p03", "\u001fx", "p03\u001f", "x")]
    [InlineData("p03", "x\n", "p03", "x\n\n")]
    public void A_separator_or_a_boundary_moved_inside_the_content_does_not_collide(string labelA, string contentA, string labelB, string contentB)
    {
        ScoutCommitment.Of(Key, Campaign, labelA, ScoutCommitmentField.Claim, contentA)
            .Should().NotBe(ScoutCommitment.Of(Key, Campaign, labelB, ScoutCommitmentField.Claim, contentB));
    }

    [Fact]
    public void The_ground_truth_commits_the_same_in_any_order_and_differently_when_a_path_changes()
    {
        var committer = new ScoutCommitter(Key, Campaign);

        committer.GroundTruth("p03", ["src/B.php", "src/A.php", "tests/C.php"]).Should().Be(committer.GroundTruth("p03", ["tests/C.php", "src/A.php", "src/B.php"]));
        committer.GroundTruth("p03", ["src\\B.php", "src/A.php"]).Should().Be(committer.GroundTruth("p03", ["src/A.php", "src/B.php"]), "a backslash path is the same file");
        committer.GroundTruth("p03", ["src/A.php", "src/B.php"]).Should().NotBe(committer.GroundTruth("p03", ["src/A.php", "src/C.php"]));
        committer.GroundTruthFile("p03", "src/A.php").Should().NotBe(committer.GroundTruthFile("p03", "src/B.php")).And.NotBe(committer.GroundTruth("p03", ["src/A.php"]), "a file's commitment is its own field");
        ScoutCommitment.GroundTruth(["b", "a"]).Should().Be("a\nb");
    }

    [Fact]
    public void A_statement_commits_the_same_with_CRLF_or_LF_surrounding_whitespace_and_either_unicode_form()
    {
        var committer = new ScoutCommitter(Key, Campaign);
        var lf = committer.Statement("p03", "Where is it decided?\nAnd why?");

        committer.Statement("p03", "Where is it decided?\r\nAnd why?").Should().Be(lf);
        committer.Statement("p03", "  Where is it decided?\nAnd why?  \n").Should().Be(lf);
        committer.Statement("p03", "café").Should().Be(committer.Statement("p03", "café"), "NFC: a precomposed and a decomposed é are one sentence");
        committer.Statement("p03", "Where is it decided?\nAnd why not?").Should().NotBe(lf);
    }

    [Fact]
    public void A_restart_reproduces_every_commitment_and_a_wrong_key_or_campaign_fails_the_verification_by_label()
    {
        var task = Task();
        var before = new ScoutCommitter(Key, Campaign).Task(task);
        var afterRestart = new ScoutCommitter(FileHashKey.Of(Enumerable.Range(0, 32).Select(i => (byte)(i * 11 + 5)).ToArray()).Ok(), Campaign);

        afterRestart.Task(task).Should().Be(before, "the same key bytes read again give the same commitments");
        afterRestart.Verified(task with { Commitments = before }).Ok().Commitments.Should().Be(before);
        afterRestart.GroundTruthFiles(task).Should().HaveCount(3).And.OnlyHaveUniqueItems();

        var wrongKey = new ScoutCommitter(OtherKey, Campaign).Verified(task with { Commitments = before }).Reason();
        var wrongCampaign = new ScoutCommitter(Key, "another").Verified(task with { Commitments = before }).Reason();
        var edited = afterRestart.Verified(task with { Commitments = before, Statement = "Where is it decided, really?" }).Reason();

        foreach (var refusal in new[] { wrongKey, wrongCampaign, edited })
        {
            refusal.Should().StartWith("task p03 (private): its public commitments are not the ones this campaign key derives")
                .And.NotContain("Vrembolix").And.NotContain("Kleebor").And.NotContain("zqvtharnish");
        }
    }

    [Fact]
    public void The_claim_digest_keeps_a_public_claims_plain_hash_and_commits_a_private_one_and_refuses_a_private_run_without_a_key()
    {
        var committer = new ScoutCommitter(Key, Campaign);
        var digest = ScoutClaimDigest.Of([Task(), ScoutStoreFixtures.FrozenTask("t01")], committer).Ok();

        digest.Of("t01", "defines the type").Should().Be(Bench.Domain.StableHash.Of("defines the type"));
        digest.Of("p03", "defines the Kleebor throttle").Should().Be(committer.Claim("p03", "defines the Kleebor throttle")).And.NotBe(Bench.Domain.StableHash.Of("defines the Kleebor throttle"));
        ScoutClaimDigest.Of([Task()], null).Reason().Should().Contain("p03").And.Contain("campaign key");
        ScoutClaimDigest.Of([ScoutStoreFixtures.FrozenTask("t01")], null).Ok().PrivateTasks.Should().BeEmpty();
    }

    private static ScoutTask Task() =>
        ScoutTask.Frozen(ScoutTaskId.Parse("p03").Ok(), "file:////wsl.localhost/Ubuntu/home/someone/git/zqvtharnish-repo", CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, "Where is the per-tenant rate limit decided for the Vrembolix gateway?", ["src/Zqvtharnish/Kleebor.php", "src/Zqvtharnish/Throttle.php", "tests/KleeborTest.php"],
            [], [], new string('c', 64), ScoutTaskPrivacy.Private, "PHP").Ok();
}
