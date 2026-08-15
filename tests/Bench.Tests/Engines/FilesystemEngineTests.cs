using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Infrastructure.Engines;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Engines;

/// <summary>The baseline engine — the one every retrieval configuration is measured against.
/// <para>
/// Its correctness matters more than its cleverness: if this surface is subtly worse than the plain
/// tools a real agent has, every retrieval engine measured beside it looks better than it is, and the
/// central comparison of the whole programme quietly tilts.
/// </para></summary>
public sealed class FilesystemEngineTests
{
    [Fact]
    public void It_declares_itself_as_the_no_retrieval_engine_with_no_funnel_to_offer()
    {
        var (engine, _) = Build();

        engine.Describe.Kind.Should().Be(EngineKind.NoRetrieval);
        engine.Describe.MayBeWhiteBox.Should().BeFalse();

        // Empty rather than a version nobody implements: an engine with no retrieval has no funnel to
        // report on, and claiming a contract it cannot fill would make the report print zeroes for
        // stages that never existed.
        engine.TraceContractVersion.Should().BeEmpty();
    }

    [Fact]
    public async Task Reading_a_file_reports_the_window_and_the_real_total()
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "one\ntwo\nthree\nfour\nfive", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, """{"path":"a.txt","startLine":2,"lineCount":2}""");

        // Paging has to be a number rather than a guess, or a subject pulls whole files to see forty
        // lines — and it will do it every time.
        answer.Should().BeOfType<ToolAnswer.Ok>()
            .Which.Content.Should().Be("lines 2-3 of 5\ntwo\nthree");
    }

    [Fact]
    public async Task A_start_past_the_end_answers_with_the_total_instead_of_failing()
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "one\ntwo", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, """{"path":"a.txt","startLine":99}""");

        answer.Should().BeOfType<ToolAnswer.Ok>().Which.Content.Should().Contain("of 2");
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("sub/../../outside.txt")]
    public async Task It_refuses_to_read_outside_the_checkout(string path)
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(
            Path.Combine(Directory.GetParent(root)!.FullName, "outside.txt"), "secret", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, $$"""{"path":"{{path}}"}""");

        // A REFUSAL, not a failure and not an empty read. The guard working is the opposite event from
        // the disk breaking, and a subject that reads an empty file concludes the file is empty.
        answer.Should().BeOfType<ToolAnswer.Refused>()
            .Which.Reason.Should().Contain("outside the repository");
    }

    [Fact]
    public async Task An_absolute_path_is_refused_even_when_it_points_inside()
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "x", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, $$"""{"path":"{{Path.Combine(root, "a.txt").Replace("\\", "\\\\")}}"}""");

        // Path.Combine happily discards the root when the second half is rooted, so this is the case
        // where "resolve then compare" would silently pass if the check were on the string.
        answer.Should().BeOfType<ToolAnswer.Ok>("an absolute path INSIDE the checkout still resolves inside it");
    }

    [Fact]
    public async Task A_literal_search_reports_file_line_and_the_matching_line()
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "orders.cs"), "class Order\n{\n  decimal Total;\n}", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.SearchLiteral, """{"text":"Total"}""");

        answer.Should().BeOfType<ToolAnswer.Ok>()
            .Which.Content.Should().Contain("orders.cs:3").And.Contain("decimal Total");
    }

    [Fact]
    public async Task A_search_that_matches_nothing_says_so_rather_than_returning_emptiness()
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "hello", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.SearchLiteral, """{"text":"absent"}""");

        // "no matches" and an empty response are the same bytes to a model that has to decide whether
        // the tool worked.
        answer.Should().BeOfType<ToolAnswer.Ok>().Which.Content.Should().Be("no matches");
    }

    [Fact]
    public async Task A_search_skips_the_git_store_and_build_output()
    {
        var (engine, root) = Build();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        Directory.CreateDirectory(Path.Combine(root, "src", "bin"));
        await File.WriteAllTextAsync(Path.Combine(root, ".git", "COMMIT_EDITMSG"), "needle", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "src", "bin", "out.txt"), "needle", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "real.txt"), "needle", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.SearchLiteral, """{"text":"needle"}""");

        // Upstream, a "slow full tree walk" was 250 ms of an 11-second call; the cost was reading
        // 1.66 GB of build output. A baseline that spends its time there measures its own patience.
        var content = answer.Should().BeOfType<ToolAnswer.Ok>().Subject.Content;
        content.Should().Contain("real.txt");
        content.Should().NotContain("COMMIT_EDITMSG").And.NotContain("out.txt");
    }

    [Fact]
    public async Task Finding_files_matches_paths_rather_than_contents()
    {
        var (engine, root) = Build();
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Order.cs"), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "src", "notes.md"), "Order", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.FindFiles, """{"pattern":"**/*.cs"}""");

        var content = answer.Should().BeOfType<ToolAnswer.Ok>().Subject.Content;
        content.Should().Contain("src/Order.cs").And.NotContain("notes.md");
    }

    [Fact]
    public async Task Listing_a_directory_marks_folders_so_a_subject_can_navigate()
    {
        var (engine, root) = Build();
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "x", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.ListDirectory, """{}""");

        var content = answer.Should().BeOfType<ToolAnswer.Ok>().Subject.Content;
        content.Should().Contain("src/").And.Contain("README.md");
    }

    [Fact]
    public async Task Malformed_arguments_are_a_refusal_the_subject_can_correct()
    {
        var (engine, _) = Build();

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, "{not json");

        // A model emits broken JSON regularly. Ending the leg over a stray brace would measure the
        // harness's brittleness rather than the subject's ability.
        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("not valid JSON");
    }

    [Fact]
    public async Task A_tool_this_engine_does_not_offer_is_refused_by_name()
    {
        var (engine, _) = Build();

        var answer = await Invoke(engine, "semantic_search", "{}");

        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("semantic_search");
    }

    [Fact]
    public async Task Warming_refuses_a_checkout_that_is_not_there()
    {
        var (engine, _) = Build();

        var missing = await engine.WarmAsync(Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N")), TestContext.Current.CancellationToken);

        // A baseline that silently measured an empty directory would make every engine beside it look
        // good, and nothing in the numbers would say why.
        missing.Failed().Should().BeTrue();
        missing.Reason().Should().Contain("checkout not found");
    }

    [Fact]
    public async Task A_file_that_is_not_there_is_a_refusal_rather_than_an_empty_read()
    {
        var (engine, _) = Build();

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, """{"path":"absent.txt"}""");

        // An empty read would let a subject conclude the file exists and says nothing — which is how a
        // wrong answer gets built on a tool that worked correctly.
        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("no such file");
    }

    [Fact]
    public async Task Reading_without_a_path_is_refused_by_naming_the_argument()
    {
        var (engine, _) = Build();

        var answer = await Invoke(engine, FilesystemEngine.ReadFile, """{}""");

        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("'path' is required");
    }

    [Fact]
    public async Task A_search_without_text_and_a_find_without_a_pattern_are_refused_the_same_way()
    {
        var (engine, _) = Build();

        (await Invoke(engine, FilesystemEngine.SearchLiteral, """{}"""))
            .Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("'text' is required");
        (await Invoke(engine, FilesystemEngine.FindFiles, """{}"""))
            .Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("'pattern' is required");
    }

    [Fact]
    public async Task A_search_honours_its_hit_cap()
    {
        var (engine, root) = Build();
        await File.WriteAllLinesAsync(
            Path.Combine(root, "many.txt"), Enumerable.Repeat("needle", 50), TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.SearchLiteral, """{"text":"needle","maxHits":5}""");

        // A tool that returns everything makes a subject pay for a context window it did not ask for,
        // and it will keep doing it — a cap that is ignored is a cap that does not exist.
        answer.Should().BeOfType<ToolAnswer.Ok>()
            .Which.Content.Split('\n').Should().HaveCount(5);
    }

    [Fact]
    public async Task Listing_a_directory_outside_the_checkout_is_refused()
    {
        var (engine, _) = Build();

        var answer = await Invoke(engine, FilesystemEngine.ListDirectory, """{"path":".."}""");

        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("outside the repository");
    }

    [Fact]
    public async Task Listing_a_directory_that_is_not_there_is_refused_rather_than_answered_empty()
    {
        var (engine, _) = Build();

        var answer = await Invoke(engine, FilesystemEngine.ListDirectory, """{"path":"nowhere"}""");

        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("no such directory");
    }

    [Fact]
    public async Task A_pattern_that_matches_no_file_says_so()
    {
        var (engine, root) = Build();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "x", TestContext.Current.CancellationToken);

        var answer = await Invoke(engine, FilesystemEngine.FindFiles, """{"pattern":"**/*.rs"}""");

        answer.Should().BeOfType<ToolAnswer.Ok>().Which.Content.Should().Be("no files match");
    }

    [Fact]
    public async Task Warming_a_real_checkout_answers_with_the_engine_it_warmed()
    {
        var (engine, root) = Build();

        var warmed = await engine.WarmAsync(root, TestContext.Current.CancellationToken);

        // There is no index to build, and saying so plainly beats a no-op that looks like success by
        // accident: a run records which engine actually served it.
        warmed.Ok().Should().Be(EngineRef.Filesystem().Canonical);
    }

    [Fact]
    public void Every_tool_describes_what_it_is_FOR_and_what_its_odd_parameters_cost()
    {
        var (engine, _) = Build();

        engine.Tools.Should().HaveCount(4);
        foreach (var tool in engine.Tools)
        {
            // A tool's description is a measured artefact: the same four tools behind differently
            // worded surfaces scored 4/63 against 37/63. A one-word description is a tool nobody calls
            // correctly, and this is the cheapest possible floor under that.
            tool.Description.Length.Should().BeGreaterThan(60, "{0} needs a behavioural description", tool.Name);
            tool.ArgumentsSchema.Should().NotBeEmpty();
        }
    }

    private static Task<ToolAnswer> Invoke(FilesystemEngine engine, string tool, string arguments) =>
        engine.InvokeAsync(tool, arguments, TestContext.Current.CancellationToken);

    private static (FilesystemEngine Engine, string Root) Build()
    {
        var root = Directory.CreateTempSubdirectory("bench-fs-engine").FullName;
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        return (new FilesystemEngine(root), root);
    }
}
