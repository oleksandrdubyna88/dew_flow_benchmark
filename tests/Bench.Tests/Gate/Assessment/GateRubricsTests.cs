using Bench.Application;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Tests.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Assessment;

/// <summary>S4.1 — the two rubrics, read from the REAL <c>prompts/gate-assess/</c> and hashed through
/// <see cref="PromptCatalog"/>: the files an operator edits are the files a verdict names.</summary>
public sealed class GateRubricsTests
{
    private static readonly string Root = Path.Combine(Repository.Root, "prompts");

    [Fact]
    public void The_catalog_holds_strict_v1_and_lenient_worth_v1_each_under_the_hash_of_its_file()
    {
        var rubrics = GateRubrics.Load(Root).Ok();
        var catalog = GateRubrics.Catalog(rubrics);

        rubrics.Select(r => (r.Rubric.Id.Value, r.Rubric.Kind)).Should().Equal([("strict-v1", RubricKind.Strict), ("lenient-worth-v1", RubricKind.LenientWorth)]);
        rubrics.Should().OnlyContain(r => catalog.Resolve(r.Rubric.Hash).Ok() == r.Rubric);
        rubrics[0].Rubric.Hash.Should().Be(StableHash.Of(rubrics[0].Text));
    }

    [Fact]
    public void The_strict_rubric_is_the_calibrations_text_with_its_load_bearing_sentences()
    {
        var strict = GateRubrics.Load(Root).Ok()[0].Text;

        strict.Should().StartWith("# Blinded assessor instructions (read-only) — STRICT rubric");
        strict.Should().Contain("`supported`: the trigger, the mechanism AND the consequence the finding states are ALL correct at that code.");
        strict.Should().Contain("Be strict: plausible-but-unverified is\n   `unresolved`, not `supported`");
        strict.Should().Contain("6. seed_hit — the seed id the finding identifies (the same TRIGGER and MECHANISM, not merely the same file), else \"none\".");
        strict.Should().Contain("Every input id must appear exactly once.");
    }

    [Fact]
    public void Editing_a_rubric_file_is_a_new_rubric_and_line_endings_are_not()
    {
        using var root = new TempRubrics();

        var lf = PromptCatalog.GateRubric(root.Path, "strict").Ok().Hash;
        File.WriteAllText(System.IO.Path.Combine(root.Path, "gate-assess", "strict.md"), "line one\r\nline two\r\n");
        var crlf = PromptCatalog.GateRubric(root.Path, "strict").Ok().Hash;
        File.WriteAllText(System.IO.Path.Combine(root.Path, "gate-assess", "strict.md"), "line one\nline 2\n");

        crlf.Should().Be(lf, "a checkout that turned LF into CRLF is the same wording, not a second rubric");
        PromptCatalog.GateRubric(root.Path, "strict").Ok().Hash.Should().NotBe(lf, "an edited file is a new rubric");
    }

    [Fact]
    public void A_missing_rubric_file_is_refused_by_path() =>
        PromptCatalog.GateRubric(Root, "strict-v9").Reason().Should().Contain("strict-v9.md");

    [Fact]
    public void Only_the_strict_rubric_is_ever_asked()
    {
        var rubrics = GateRubrics.Load(Root).Ok();

        GateRubrics.Asked(rubrics, "strict-v1").Ok().Rubric.Kind.Should().Be(RubricKind.Strict);
        GateRubrics.Asked(rubrics, "lenient-worth-v1").Reason().Should().Contain("never asked");
    }

    private sealed class TempRubrics : IDisposable
    {
        public TempRubrics()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "gate-assess"));
            File.WriteAllText(System.IO.Path.Combine(Path, "gate-assess", "strict.md"), "line one\nline two\n");
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bench-rubrics-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
