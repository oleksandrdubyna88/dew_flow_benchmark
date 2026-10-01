using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Probes;
using Bench.Domain.Probes;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Probes;
using Bench.Tests.Infrastructure;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2b (S5.2's "a reader found wrong is fixed RED-first on that transcript"): the readers against the LIVE transcripts the
/// first runs of 2026-10-01 produced — <c>tests/Bench.Tests/Fixtures/probes/</c>, named by CLI and version. Each test here was
/// watched failing for the real reason before its fix; the README of the fixture folder says which file came from where.</summary>
public sealed class ProbeLiveTranscriptTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();
    private readonly List<FakeCli> _fakes = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Finding 5: codex-cli 0.156.1 prints a <c>web_search</c> item with TWO <c>id</c> properties in one object; a reader that
    /// builds a dictionary threw <c>ArgumentException: An item with the same key has already been added. Key: id</c> and the leg faulted.</summary>
    [Fact]
    public void Codex_events_with_a_duplicate_id_key_are_read_not_thrown_on()
    {
        var live = ProbeVerdictsTests.Fixture("codex-0.156.1-web-search.jsonl");

        ProbeTranscripts.Answer(ProbeRuntime.Codex, live).Should().Contain("Latest `@openai/codex` version: **0.159.3**.", "both agent_message items, the model's preamble first");
        ProbeTranscripts.Read(ProbeRuntime.Codex, live).WebSearchUsed.Should().Be(ProbeFact.Yes, "three web_search items in a completed turn");
    }

    /// <summary>Finding 3: agy 1.2.14's stream is <c>{"event":"init"|"step_update"|"result"}</c> with the answer in <c>result.response</c>
    /// and tool steps as <c>step_update.step_type = "tool"</c> — not the synthetic <c>type/message/tool_use</c> shape S1 guessed.</summary>
    [Fact]
    public void Antigravity_live_stream_is_read_for_its_answer_and_its_tool_steps()
    {
        ProbeTranscripts.Answer(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-1.2.14-read-inside.ndjson")).Should().Be("IN-d5fe02153e26");
        ProbeTranscripts.Read(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-1.2.14-read-inside.ndjson")).WebSearchUsed.Should().Be(ProbeFact.No);
        ProbeTranscripts.Read(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-1.2.14-web-search.ndjson")).WebSearchUsed.Should().Be(ProbeFact.Yes, "a search_web step, then a read_url_content step");
        ProbeTranscripts.Answer(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-1.2.14-web-search.ndjson")).Should().BeEmpty("headless agy auto-denied read_url and answered nothing");
    }

    /// <summary>Finding 2: <c>--output-format json</c> is blind — the envelope of the live read-denied cell (01a0f804-2369-740a…) carried
    /// the canary in <c>result</c> with <c>permission_denials = []</c> and <c>num_turns = 6</c>, and nothing says which tool read it. Such an
    /// envelope reads NOT CAPTURED for every transcript fact, never <i>no</i>.</summary>
    [Fact]
    public void The_json_envelope_is_blind_and_reads_not_captured_never_no()
    {
        var envelope = ProbeVerdictsTests.Fixture("claude-2.1.258-json-read-denied.json");

        ProbeTranscripts.Answer(ProbeRuntime.Claude, envelope).Should().Be("OUT-5d39fc027929", "the canary came back through a tool the envelope never names");
        ProbeTranscripts.Read(ProbeRuntime.Claude, envelope).Should().Be(TranscriptEvidence.NotCaptured, "no tool is visible in it");
    }

    /// <summary>Finding 2: the stream (<c>--output-format stream-json --verbose</c>) shows the tools: the init event's offered list, each
    /// <c>tool_use</c> block, each tool result — here a <c>Read</c> of the canary refused as "No such tool available", which the envelope's
    /// <c>permission_denials</c> (still <c>[]</c>) never showed.</summary>
    [Fact]
    public void The_claude_stream_shows_the_read_attempt_the_envelope_hid()
    {
        var stream = ProbeVerdictsTests.Fixture("claude-2.1.258-denylist-read-denied.ndjson");

        ProbeTranscripts.Answer(ProbeRuntime.Claude, stream).Should().StartWith("The Read tool is disabled for this session");
        ProbeTranscripts.Read(ProbeRuntime.Claude, stream).ReadAttempted.Should().Be(ProbeFact.Yes, "a Read tool_use named canary.txt and was refused");
        ProbeTranscripts.Read(ProbeRuntime.Claude, ProbeVerdictsTests.Fixture("claude-2.1.258-denylist-web-search.ndjson")).WebSearchUsed
            .Should().Be(ProbeFact.Yes, "WebSearch and WebFetch tool_use blocks — while usage.server_tool_use stayed at 0");
    }

    /// <summary>Finding 4: coai-mcp 0.40.3 <c>--probe-api</c> prints a JSON object; the live grok run answered 403 on every completion case
    /// ("…has either used all available credits or reached its monthly spending limit…") and 400 on the deliberate <c>wrong_key</c> case.</summary>
    [Fact]
    public void The_probe_api_report_is_json_and_a_spent_account_reads_account_out_with_the_wrong_key_case_excluded()
    {
        var live = ProbeApiOutput.Read(ProbeVerdictsTests.Fixture("coai-mcp-0.40.3-probe-api-grok-403.json"));

        live.Statuses.Should().HaveCount(9, "nine completion cases; the deliberate wrong_key case (400) is not one");
        live.Statuses.Should().OnlyContain(s => s == 403);
        ProbeApiOutput.AccountOut(live).Should().Be(ProbeFact.Yes);
        ProbeVerdicts.ApiReachable(0, live).Reachable.Should().Be(ProbeFact.No, "the endpoint answered, no completion case was a 200");
    }

    /// <summary>Finding 3: every live agy cell exited 2 — <c>Error: --print took "--model" as its prompt</c>. The launch coai measured is
    /// <c>--print= --input-format stream-json --output-format stream-json --mode plan --model m</c> with the prompt as one NDJSON line on stdin.</summary>
    [Fact]
    public void Antigravity_is_launched_as_coai_launches_it()
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliAntigravity, "gemini-3.1-pro-high", new AgentAskOptions { JsonEvents = true, Sandbox = AgentSandbox.ReadOnly }, []).Ok();

        argv.Should().StartWith(["--print=", "--input-format", "stream-json", "--output-format", "stream-json", "--mode", "plan"]);
        argv.Should().ContainInConsecutiveOrder("--model", "gemini-3.1-pro-high");
        argv.Should().NotContain("--print", "a bare --print takes the next token as its prompt (agy 1.2.14)");
    }

    /// <summary>Finding 2: claude's transcript is the stream, and the stream needs <c>--verbose</c> in print mode.</summary>
    [Fact]
    public void Claude_is_launched_for_the_stream_transcript()
    {
        CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", new AgentAskOptions { JsonEvents = true }, []).Ok()
            .Should().ContainInConsecutiveOrder("--output-format", "stream-json", "--verbose");
    }

    /// <summary>Finding 1: a claude subject carries its confinement mode, frozen on the run.</summary>
    [Fact]
    public void A_claude_subject_names_its_confinement_mode()
    {
        const string file = """
            { "subjects": [ { "id": "claude-sonnet-allowlist", "runtime": "claude", "model": "sonnet", "executableRef": "BENCH_CLAUDE", "confinement": "allowlist" } ] }
            """;

        ProbeSubjectsFile.Read(file).Ok().Single().Describe.Should().Contain("allowlist");
    }

    /// <summary>Finding 6: the two faulted codex cells left NO artefacts — stdout and stderr were never committed because the reader threw
    /// first. The raw evidence is committed BEFORE any parsing, with the argv and the prompt beside it.</summary>
    [Fact]
    public async Task The_raw_transcript_the_argv_and_the_prompt_are_committed_before_anything_is_parsed()
    {
        var (runner, run, subject) = Rig("codex", new JsonObject { ["stdoutFile"] = Path.Combine(AppContext.BaseDirectory, "Fixtures", "probes", "codex-0.156.1-web-search.jsonl") });

        var result = await runner.RunAsync(run, subject, Claimed(run, subject, ProbeKind.WebSearch), Ct);

        var files = Directory.EnumerateFiles(Path.Combine(_root.Path, "probes"), "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
        files.Should().Contain(["stdout.txt", "stderr.txt", "argv.json", "prompt.txt"], "the raw evidence is on disk whatever the reader did with it");
        var facts = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;
        facts.Kind.Should().Be(ProbeAttemptKind.Answered);
        facts.ToolEvidence.Should().Be(ProbeFact.Yes);
        facts.AnswerCurrent.Should().Be(ProbeFact.No, "the live answer said 0.159.3 against this rig's 0.52.0 oracle — the comparator saw a version");
        facts.ShellUsed.Should().Be(ProbeFact.No);
        files.Should().Contain("tools.json");
    }

    private (CliProbeRunner Runner, ProbeRun Run, ProbeSubject Subject) Rig(string runtime, JsonObject script)
    {
        var fake = new FakeCli(script);
        _fakes.Add(fake);
        var subject = ProbeSubject.Parse($"{runtime}-fake-{_fakes.Count}", runtime, fake.Model, "BENCH_FAKE_CLI", runtime == "claude" ? "denylist" : string.Empty).Ok();
        var run = ProbeRun.Planned(Guid.CreateVersion7(), ProbeStoreFixtures.Oracle(), [subject], 1, DateTimeOffset.UtcNow).Ok();
        var runner = new CliProbeRunner(
            new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance), new ProbeFixtures(_root.Sibling("work")), new ProbeArtifacts(_root.Path),
            new CliProbeSettings(new Dictionary<string, string> { [subject.Id.Value] = FakeCli.Executable }, TimeSpan.FromSeconds(60)),
            NullLogger<CliProbeRunner>.Instance);

        return (runner, run, subject);
    }

    private static ProbeCell Claimed(ProbeRun run, ProbeSubject subject, ProbeKind probe) =>
        ProbeCellLifecycle.Claim(
            ProbeCell.Pending(Guid.CreateVersion7(), run.Id, new ProbeMatrixCell(probe, subject.Id, 1, 0, 0)), WorkerIdentity.Here("test"), DateTimeOffset.UtcNow, ProbeStoreFixtures.Pin()).Ok();

    public void Dispose()
    {
        foreach (var fake in _fakes)
        {
            fake.Dispose();
        }

        _root.Dispose();
        Gate.Driver.GateDriverRig.DeleteSiblings(_root);
    }
}
