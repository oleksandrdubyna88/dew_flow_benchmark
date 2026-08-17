using Bench.Application;
using Bench.Tests.Cli;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>The prompt catalog on disk.
/// <para>
/// Asserted against the REAL <c>prompts/</c> directory rather than a fixture, because the thing worth
/// guarding is that the files an operator edits are the files a run sends — a fixture would prove the loader
/// agrees with itself.
/// </para>
/// <para>
/// And it is worth guarding at all because a prompt is the largest measured axis in this system: rewriting one
/// ordering instruction moved a score <b>16.5 points of 63</b> where swapping 4 tools for 18 moved 1.
/// </para></summary>
public sealed class PromptCatalogTests
{
    private static readonly string Root = Path.Combine(Repository.Root, "prompts");
    private static readonly RepoUrl Target = RepoUrl.Parse("https://github.com/dotnet/aspnetcore.git").Ok();
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    [Theory]
    [InlineData("code-lookup")]
    [InlineData("semantic-intent")]
    [InlineData("pr-diff")]
    [InlineData("bug-root-cause")]
    [InlineData("adversarial")]
    public void Every_group_this_catalog_claims_has_both_of_its_files(string group)
    {
        var authored = PromptCatalog.Author(Root, Brief(group)).Ok();
        var reviewed = PromptCatalog.Review(Root, Review(group)).Ok();

        // A group in the list with a file missing is a run that fails on its first candidate, hours in.
        authored.Text.Should().Contain(group);
        authored.Source.Should().Be($"author/{group}");
        reviewed.Source.Should().Be($"review/{group}");
    }

    [Fact]
    public void The_rendered_prompt_carries_the_target_and_the_pinned_commit()
    {
        var rendered = PromptCatalog.Author(Root, Brief("code-lookup")).Ok();

        rendered.Text.Should().Contain(Target.Value).And.Contain(Commit.Value);
        rendered.Text.Should().Contain("exactly 5 question");
    }

    [Fact]
    public void A_template_with_a_placeholder_left_over_is_REFUSED()
    {
        using var broken = new TempCatalog("author", "code-lookup", "Write about {{repository}} at {{commit}}.");

        var refused = PromptCatalog.Author(broken.Root, Brief("code-lookup"));

        // An agent answers a template as readily as a brief — plausibly, about no particular repository — and
        // the questions it writes look exactly like correct ones. A whole batch of unattributable candidates,
        // produced by a defect nothing else surfaces.
        refused.Reason().Should().Contain("{{repository}}");
        refused.Reason().Should().Contain("would look exactly like correct ones");
    }

    [Fact]
    public void The_hash_is_of_the_TEMPLATE_so_two_renderings_of_one_prompt_share_it()
    {
        var five = PromptCatalog.Author(Root, Brief("adversarial") with { Count = 5 }).Ok();
        var twenty = PromptCatalog.Author(Root, Brief("adversarial") with { Count = 20 }).Ok();

        // "These hundred questions came from that prompt" is the fact being stored, and it is about the prompt
        // rather than about one call's substitutions.
        five.Hash.Should().Be(twenty.Hash);
        five.Text.Should().NotBe(twenty.Text);
    }

    [Fact]
    public void Changing_the_SHARED_contract_changes_every_groups_hash()
    {
        var before = PromptCatalog.Author(Root, Brief("pr-diff")).Ok().Hash;

        using var edited = new TempCatalog(Root);
        edited.Append("author/_shared.md", "\n\nOne more rule that changes what an author does.\n");

        // Correct rather than inconvenient: the prompt DID change. Five per-group copies of the contract would
        // have let a shared-rule edit slip through with four identities unchanged.
        PromptCatalog.Author(edited.Root, Brief("pr-diff")).Ok().Hash.Should().NotBe(before);
    }

    [Fact]
    public void The_group_whose_authoring_needs_a_build_is_refused_by_NAME()
    {
        var refused = PromptCatalog.Author(Root, Brief("code-writing"));

        // Its three gates — the bug reproduces, the reference fix works, the tree is rebuilt to the buggy
        // state — need a sandbox worktree and a build. Refused here rather than authored badly.
        refused.Reason().Should().Contain("code-writing is deliberately absent");
        PromptCatalog.Groups.Should().NotContain("code-writing");
    }

    [Fact]
    public void A_brief_asking_for_no_questions_is_refused() =>
        PromptCatalog.Author(Root, Brief("code-lookup") with { Count = 0 })
            .Reason().Should().Contain("at least one question");

    [Fact]
    public void A_review_brief_with_no_question_in_it_is_refused() =>
        PromptCatalog.Review(Root, Review("code-lookup") with { QuestionJson = "  " })
            .Reason().Should().Contain("verdict about nothing");

    [Fact]
    public void The_author_contract_demands_JSON_and_nothing_else()
    {
        var rendered = PromptCatalog.Author(Root, Brief("semantic-intent")).Ok().Text;

        // The parser refuses a malformed answer rather than repairing it, so the contract has to be explicit
        // about the one thing that makes an answer parseable at all.
        rendered.Should().Contain("nothing else").And.Contain("no markdown fence");
        rendered.Should().Contain("Never invent a date", "a guessed seed date reads as safe, which is the lie");
    }

    private static AuthoringBrief Brief(string group) => new(group, "a title", Target, Commit, 5);

    private static ReviewBrief Review(string group) => new(group, "a title", Target, Commit, """{"id":"q1"}""");

    /// <summary>A copy of the real catalog that a test may edit, so an assertion about a CHANGED prompt never
    /// changes the repository's own files.</summary>
    private sealed class TempCatalog : IDisposable
    {
        public TempCatalog(string source)
        {
            Root = Path.Combine(Path.GetTempPath(), $"bench-prompts-{Guid.NewGuid():N}");
            Copy(new DirectoryInfo(source), new DirectoryInfo(Root));
        }

        public TempCatalog(string role, string group, string template)
        {
            Root = Path.Combine(Path.GetTempPath(), $"bench-prompts-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(Root, role));
            File.WriteAllText(Path.Combine(Root, role, "_shared.md"), template);
            File.WriteAllText(Path.Combine(Root, role, $"{group}.md"), "# brief\n");
        }

        public string Root { get; }

        public void Append(string relative, string text) =>
            File.AppendAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)), text);

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static void Copy(DirectoryInfo from, DirectoryInfo to)
        {
            to.Create();

            foreach (var file in from.GetFiles())
            {
                file.CopyTo(Path.Combine(to.FullName, file.Name));
            }

            foreach (var directory in from.GetDirectories())
            {
                Copy(directory, new DirectoryInfo(Path.Combine(to.FullName, directory.Name)));
            }
        }
    }
}
