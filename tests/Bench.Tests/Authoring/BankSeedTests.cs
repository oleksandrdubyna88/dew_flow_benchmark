using Bench.Application;
using Bench.Application.Bank;
using Bench.Tests.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>The committed shape of the bank: <c>data/bank-seed.json</c>.
/// <para>
/// It exists as a file, and is asserted here, because "how many question groups does this project have" was
/// answered from memory once and the answer was wrong by one. There are SIX. Five can be authored by a machine;
/// the sixth needs a build, so it is a row nothing writes into yet rather than a group that does not exist.
/// </para></summary>
public sealed class BankSeedTests
{
    private static readonly string Seed = Path.Combine(Repository.Root, "data", "bank-seed.json");

    private static readonly string[] Expected =
        ["code-lookup", "semantic-intent", "pr-diff", "bug-root-cause", "adversarial", "code-writing"];

    [Fact]
    public void The_seed_file_names_all_SIX_groups_in_order()
    {
        var file = BankImport.Read(File.ReadAllText(Seed)).Ok();

        file.Groups.Select(g => g.Key).Should().Equal(Expected);
        file.Groups.Select(g => g.Ordinal).Should().Equal([1, 2, 3, 4, 5, 6],
            "the operator quotes ordinals — 'group 1, questions 1–10' — so they are assigned, not derived");
    }

    [Fact]
    public void The_sixth_group_is_in_the_BANK_and_deliberately_not_in_the_prompt_catalog()
    {
        var file = BankImport.Read(File.ReadAllText(Seed)).Ok();

        // The distinction this test exists to keep: `code-writing` is a real group of this benchmark, and it is
        // the one `bench questions author` refuses by name. Its questions need a bug that reproduces, a
        // reference fix that works, and a tree rebuilt to the buggy state — a sandbox worktree and a build,
        // which `todo/PLAN_code_lane.md` owns. Absent from the bank it would silently become five groups.
        file.Groups.Select(g => g.Key).Should().Contain("code-writing");
        PromptCatalog.Groups.Should().NotContain("code-writing");
        PromptCatalog.Groups.Should().BeEquivalentTo(Expected.Except(["code-writing"]));
    }

    [Fact]
    public void The_seeded_reviewer_slots_name_NO_model()
    {
        var file = BankImport.Read(File.ReadAllText(Seed)).Ok();

        file.Reviewers.Should().HaveCount(3);

        // Binding a model to a slot is a local decision with a stated cost, and three slots on one model is
        // one opinion sampled three times. A committed default would read as this project's recommendation.
        file.Reviewers.Should().OnlyContain(r => r.Model.Length == 0);
    }

    [Fact]
    public void The_seed_file_carries_no_questions()
    {
        // It seeds the bank's SHAPE. Questions arrive by `author`, by `import` of a real set, or from a person —
        // a committed set would become the default measurement and nobody would notice they had inherited it.
        BankImport.Read(File.ReadAllText(Seed)).Ok().Questions.Should().BeEmpty();
    }
}
