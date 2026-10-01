using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>A subject is frozen on the run as REFERENCES (D4): the executable is the NAME of an environment variable, and a
/// path or anything key-shaped is refused by name — the database is published unedited, and a path in it is a machine's
/// identity leaving with the results, a key somebody's account.</summary>
public sealed class ProbeSubjectTests
{
    [Theory]
    [InlineData(@"C:\tools\claude.cmd", "PATH")]
    [InlineData("/usr/local/bin/codex", "PATH")]
    [InlineData("tools/agy", "PATH")]
    [InlineData("sk-ant-api03-abcdefghijklmnopqrstuvwxyz0123456789", "KEY")]
    [InlineData("AIzaSyD9f8g7h6j5k4l3m2n1o0p9q8r7s6t5u4v3w", "KEY")]
    [InlineData("xai-7f8e9d0c1b2a3948576a5b4c3d2e1f0a", "KEY")]
    [InlineData("bench_claude", "environment variable NAME")]
    [InlineData("", "environment variable NAME")]
    public void A_path_or_a_key_shaped_executable_reference_is_refused_by_name(string executableRef, string shape)
    {
        ProbeSubject.Parse("claude-sonnet", "claude", "claude-sonnet-4-5", executableRef).Reason()
            .Should().Contain(shape, "the refusal names the shape, so the operator learns the rule rather than 'invalid'");
    }

    [Fact]
    public void A_name_is_accepted_and_stored_as_given()
    {
        var subject = ProbeSubject.Parse(" claude-sonnet ", "Claude", " claude-sonnet-4-5 ", "BENCH_CLAUDE").Ok();

        subject.Id.Value.Should().Be("claude-sonnet");
        subject.Runtime.Should().Be(ProbeRuntime.Claude);
        subject.ModelId.Should().Be("claude-sonnet-4-5");
        subject.ExecutableRef.Should().Be("BENCH_CLAUDE");
    }

    [Theory]
    [InlineData("https://api.x.ai/v1")]
    [InlineData("/models/grok")]
    [InlineData("")]
    public void A_model_id_that_is_a_url_a_path_or_nothing_is_refused(string modelId)
    {
        ProbeSubject.Parse("grok-api", "api", modelId, "BENCH_GATE_COAI_EXE").Reason().Should().Contain("model id");
    }

    [Fact]
    public void An_unknown_runtime_word_is_refused_naming_the_words()
    {
        ProbeSubject.Parse("gemini", "gemini", "gemini-3.1-pro", "BENCH_GEMINI").Reason()
            .Should().Contain("'gemini' is not a probe runtime").And.Contain("claude, codex, antigravity, api");
    }

    [Fact]
    public void The_subjects_file_reads_every_subject_and_refuses_an_unknown_field_a_duplicate_and_a_missing_array()
    {
        const string file = """
            {
              "subjects": [
                { "id": "claude-sonnet", "runtime": "claude", "model": "claude-sonnet-4-5", "executableRef": "BENCH_CLAUDE" },
                { "id": "codex-astra", "runtime": "codex", "model": "gpt-6-astra", "executableRef": "BENCH_CODEX" },
                { "id": "grok-api", "runtime": "api", "model": "grok-4.7", "executableRef": "BENCH_GATE_COAI_EXE" }
              ]
            }
            """;

        var subjects = ProbeSubjectsFile.Read(file).Ok();

        subjects.Select(s => s.Id.Value).Should().Equal(["claude-sonnet", "codex-astra", "grok-api"]);
        subjects[1].Runtime.Should().Be(ProbeRuntime.Codex);

        ProbeSubjectsFile.Read(file.Replace("\"executableRef\": \"BENCH_CODEX\"", "\"executablePath\": \"BENCH_CODEX\"", StringComparison.Ordinal)).Reason()
            .Should().Contain("'executablePath'").And.Contain("codex-astra", "a field the reader does not know is a typo the run would otherwise swallow");
        ProbeSubjectsFile.Read(file.Replace("codex-astra", "claude-sonnet", StringComparison.Ordinal)).Reason().Should().Contain("'claude-sonnet' is listed twice");
        ProbeSubjectsFile.Read("""{ "items": [] }""").Reason().Should().Contain("'subjects'");
        ProbeSubjectsFile.Read("not json").Reason().Should().Contain("not JSON");
        ProbeSubjectsFile.Read(file.Replace("BENCH_CLAUDE", "/usr/bin/claude", StringComparison.Ordinal)).Reason().Should().Contain("PATH");
    }

    [Fact]
    public void A_planned_run_freezes_the_subjects_and_refuses_an_empty_list_or_a_duplicate()
    {
        var oracle = ProbeOracle.Parse("0.52.0", OracleSource.Registry).Ok();
        var subjects = ProbeMatrixTests.Subjects(("claude-sonnet", "claude"), ("codex-astra", "codex"));

        var run = ProbeRun.Planned(Guid.CreateVersion7(), oracle, subjects, repeats: 3, DateTimeOffset.UnixEpoch).Ok();

        run.Subjects.Should().Equal(subjects);
        run.Subjects.Should().OnlyContain(s => s.ExecutableRef == "BENCH_X", "the run stores names, never values");
        run.ArtifactsPruned.Should().BeFalse();
        run.Subject(subjects[1].Id).Ok().Should().Be(subjects[1]);
        run.Subject(ProbeSubjectId.Parse("nobody").Ok()).Reason().Should().Contain("no subject 'nobody'");
        ProbeRun.Planned(Guid.CreateVersion7(), oracle, [], 3, DateTimeOffset.UnixEpoch).Reason().Should().Contain("at least one subject");
        ProbeRun.Planned(Guid.CreateVersion7(), oracle, [subjects[0], subjects[0]], 3, DateTimeOffset.UnixEpoch).Reason().Should().Contain("listed twice");
        ProbeRun.Planned(Guid.CreateVersion7(), oracle, subjects, 0, DateTimeOffset.UnixEpoch).Reason().Should().Contain("repeats must be at least 1");
    }

    [Theory]
    [InlineData("0.52.0", "0.52.0")]
    [InlineData("v0.52.0", "0.52.0")]
    [InlineData("1.0.0-alpha.3", "1.0.0-alpha.3")]
    public void An_oracle_is_a_semver_with_or_without_a_v(string text, string version)
    {
        ProbeOracle.Parse(text, OracleSource.Manual).Ok().Should().Match<ProbeOracle>(o => o.Version == version && o.Source == OracleSource.Manual);
        ProbeOracle.Parse("latest", OracleSource.Registry).Reason().Should().Contain("not a version");
        ProbeOracle.Parse("", OracleSource.Registry).Reason().Should().Contain("not a version");
    }
}
