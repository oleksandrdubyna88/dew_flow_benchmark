using Bench.Domain.Authoring;
using Bench.Domain.Suites;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Suites;

/// <summary>Whether a question's seed date is what the repository says.
/// <para>
/// Every case is the real one from 2026-08-18: handed 345 lines of history and told to copy the dates verbatim,
/// an author cited three commits that all exist, whose subjects match its questions, and stored every one of
/// them a day early. Systematically off by one — an author does not copy a date, it reasons about one.
/// </para></summary>
public sealed class SeedCheckTests
{
    private static readonly DateOnly Landed = new(2026, 8, 17);

    [Fact]
    public void A_seed_dated_a_day_EARLY_is_caught_with_both_dates_named()
    {
        var defect = SeedCheck.Verify(Seed("5d6aec9", new DateOnly(2026, 8, 16)), Repository()).Should().ContainSingle().Subject;

        // The exact defect. A day matters because the date is the memorisation check's only input: shifted the
        // wrong way it turns "may recall" into "clear", which is the one direction that cannot be recovered.
        defect.Fault.Should().Be(SeedFault.WrongDate);
        defect.Describe.Should().Contain("2026-08-16").And.Contain("2026-08-17");
    }

    [Fact]
    public void A_seed_dated_exactly_right_passes() =>
        SeedCheck.Verify(Seed("5d6aec9", Landed), Repository()).Should().BeEmpty();

    [Fact]
    public void A_commit_that_is_not_in_the_repository_is_named_as_such()
    {
        var defect = SeedCheck.Verify(Seed("deadbee", Landed), Repository()).Should().ContainSingle().Subject;

        defect.Fault.Should().Be(SeedFault.NoSuchCommit);
        defect.Describe.Should().Contain("no such commit");
    }

    [Fact]
    public void An_UNSTATED_seed_claims_nothing_and_so_cannot_be_wrong()
    {
        // The honest absence the contract asks for when an author cannot establish a date. Flagging it would
        // punish the one behaviour the shared contract explicitly wants.
        SeedCheck.Verify(new QuestionSeed("unstated", string.Empty, default), Repository()).Should().BeEmpty();
    }

    [Fact]
    public void A_commit_seed_with_no_DATE_is_not_a_wrong_date()
    {
        SeedCheck.Verify(new QuestionSeed("commit", "5d6aec9", default), Repository()).Should().BeEmpty(
            "a seed that states a commit but no date makes no claim about when — and the commit does exist");
    }

    [Theory]
    [InlineData("pull-request")]
    [InlineData("issue")]
    [InlineData("member")]
    public void Only_a_COMMIT_seed_is_checkable_this_way(string kind)
    {
        // A PR number lives on a forge, an issue likewise, a member has no date of its own. Checking them here
        // would mean inventing a lookup that cannot exist and reporting its silence as a defect.
        SeedCheck.Verify(new QuestionSeed(kind, "123", new DateOnly(2000, 1, 1).ToDateTime(default, DateTimeKind.Utc)), Repository())
            .Should().BeEmpty();
    }

    [Fact]
    public void A_date_read_from_JSON_in_a_NON_UTC_offset_is_the_DAY_it_was_written()
    {
        // How a seed actually arrives. `"at": "2026-08-17"` deserialises to midnight in the READING machine's
        // offset, and every other test in this class builds its date already in UTC — which is exactly why none
        // of them could see the shift that made two authors look like they dated a day early for a whole batch.
        var written = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.FromHours(2));

        SeedCheck.Verify(QuestionSeed.Written("commit", "5d6aec9", written), Repository()).Should().BeEmpty(
            "the author wrote the day the repository says, whatever offset this machine reads it in");
    }

    [Fact]
    public void A_day_early_written_in_a_non_UTC_offset_is_STILL_caught()
    {
        // The other half: fixing the false positive must not cost the true one.
        var written = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.FromHours(2));

        var defect = SeedCheck.Verify(QuestionSeed.Written("commit", "5d6aec9", written), Repository())
            .Should().ContainSingle().Subject;

        defect.Fault.Should().Be(SeedFault.WrongDate);
        defect.Describe.Should().Contain("2026-08-16").And.Contain("2026-08-17");
    }

    private static QuestionSeed Seed(string reference, DateOnly at) =>
        new("commit", reference, at.ToDateTime(default, DateTimeKind.Utc));

    private static Func<string, CommitFact> Repository() =>
        reference => reference == "5d6aec9" ? CommitFact.On(Landed) : CommitFact.Unknown;
}
