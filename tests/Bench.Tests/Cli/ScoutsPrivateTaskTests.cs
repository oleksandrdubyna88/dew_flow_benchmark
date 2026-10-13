using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Diagnostics;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Persistence;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Probes.Driver;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>THE PRIVATE-TASK SCENARIO (plan §5 E2 "Private tasks", §3.13, D10): ONE synthetic private task whose statement, paths, an added
/// identifier, a claim and its repository url carry planted strings that are on NO deny-list, driven through every verb and every public
/// writer — <c>tasks</c>, <c>run</c> (the fake claude), <c>score --handcheck</c>, <c>assess</c> (the fake codex), <c>status</c>, <c>report</c>,
/// <c>report --json</c>, the <c>cells.jsonl</c> and RESULTS writers, the public export, the log sink — and through every error path: malformed
/// candidates, a missing mirror, an I/O failure injected into the tree and into the artefact root, an unhandled exception through the CLI
/// entry. No planted string reaches stdout, stderr, a public file, an export row or a log file; the local-only files (the private tasks file,
/// the hand-check companion, the withheld detail, the fault file) DO carry them, which is how the test knows its plants are alive.
/// <para>Also the key's lifecycle through the verbs: <c>key init</c> inside a checkout refused; a later verb without the key refused (4) with
/// nothing created; a mismatch (an edited statement, a wrong key) refused by label.</para></summary>
[Collection("postgres")]
public sealed class ScoutsPrivateTaskTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Campaign = "scouts-test-campaign-2026";

    private const string Statement = "Where is the per-tenant rate limit decided for the Vrembolix gateway?";

    private const string Claim = "defines the Kleebor throttle window";

    private static readonly string[] Truth = ["src/Zqvtharnish/Kleebor.php", "src/Zqvtharnish/Throttle.php", "tests/KleeborTest.php"];

    /// <summary>Everything the task holds that must stay local: the statement's own word, the paths, the identifier the diff added, the claim's
    /// phrase, and the repository's url and root. None is on a deny-list.</summary>
    private static string[] Planted(Rig rig) => ["Vrembolix", "Zqvtharnish", "Kleebor", "Throttle.php", "kleeborThrottleWindow", "throttle window", rig.Repo.Url, rig.Repo.Root];

    private sealed record Rig(ScoutsCliSetup Setup, DatedGitRepo Repo, string Key, string PrivateFile, string Candidates, string Cutoffs, string Faults) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Repo.Dispose();
            await Setup.DisposeAsync();
        }
    }

    private static async Task<Rig> RigAsync(PostgresFixture postgres, string? repositoryUrl = null)
    {
        var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 1);
        var repo = new DatedGitRepo(Ct);
        await repo.InitAsync(("src/Other.php", "<?php class Other {}\n"), ("init", "2026-05-01T10:00:00+00:00"));
        var parent = await repo.CommitManyAsync(
            [("src/Zqvtharnish/Kleebor.php", "<?php class Kleebor {}\n"), ("src/Zqvtharnish/Throttle.php", "<?php class Throttle {}\n"), ("tests/KleeborTest.php", "<?php class KleeborTest {}\n"), ("todo/PLAN_kleebor.md", "# plan\n")],
            ("p", "2026-06-01T10:00:00+00:00"));
        var fix = await repo.CommitManyAsync(
            [("src/Zqvtharnish/Kleebor.php", "<?php class Kleebor { public int $kleeborThrottleWindow = 3; }\n"), ("src/Zqvtharnish/Throttle.php", "<?php class Throttle { public int $kleeborThrottleWindow; }\n"),
             ("tests/KleeborTest.php", "<?php class KleeborTest { public int $kleeborThrottleWindow; }\n"), ("todo/PLAN_kleebor.md", "# done\n")],
            ("s", "2026-06-02T10:00:00+00:00"));
        var files = setup.Root.Sibling("private-files");
        Directory.CreateDirectory(files);
        var candidates = Path.Combine(files, "candidates.json");
        File.WriteAllText(candidates, CandidatesJson(parent, fix, repositoryUrl ?? repo.Url));
        var cutoffs = Path.Combine(files, "cutoffs.json");
        File.WriteAllText(cutoffs, """
            { "models": [
              { "modelId": "claude-opus-5-5", "cutoff": "2026-03-31", "source": "https://example.invalid/models", "readOn": "2026-10-12" },
              { "modelId": "claude-sonnet-5-5", "cutoff": "2026-01-31", "source": "https://example.invalid/models", "readOn": "2026-10-12" } ] }
            """);
        var key = Path.Combine(setup.Root.Sibling("keys"), "scouts.key");
        ScoutCampaignKey.Create(key).Ok();

        return new Rig(setup, repo, key, Path.Combine(files, "tasks.private.json"), candidates, cutoffs, setup.Root.Sibling("faults"));
    }

    /// <summary>Five private candidates on the same change (S7: a repository needs 5 admissible candidates), listed as squash-merged PRs whose
    /// numbers are assigned by their hash order under the campaign seed — <c>p03</c> the number that sorts FIRST (the main task, whatever the
    /// clone's folder is called), <c>p01</c> the second (its reserve). <c>p03</c> is listed first, so a refusal that names the first candidate names it.</summary>
    private static string CandidatesJson(string parent, string fix, string repository, string parentOverride = "")
    {
        var numbers = Numbers(repository);

        return new JsonObject
        {
            ["candidates"] = new JsonArray([.. Labels.Select(label => (JsonNode)new JsonObject
            {
                ["id"] = label,
                ["repository"] = repository,
                ["parent"] = parentOverride.Length > 0 ? parentOverride : parent,
                ["fix"] = fix,
                ["kind"] = "where-decided",
                ["statement"] = Statement,
                ["planFile"] = "todo/PLAN_kleebor.md",
                ["private"] = true,
                ["language"] = "PHP",
                ["mergeStyle"] = "pr-squash",
                ["number"] = numbers[label],
            })]),
        }.ToJsonString();
    }

    private static readonly string[] Labels = ["p03", "p01", "p02", "p04", "p05"];

    /// <summary>PR numbers 1..5 in their draw order under the seed (<c>sha256(campaign|repo#n)</c> ascending), handed to the labels in
    /// <see cref="Labels"/>' order.</summary>
    private static IReadOnlyDictionary<string, int> Numbers(string repository)
    {
        var shortName = ScoutRepository.ShortName(repository);
        var ordered = Enumerable.Range(1, 5).OrderBy(n => Bench.Domain.StableHash.Of($"{Campaign}|{shortName}#{n}"), StringComparer.Ordinal).ToList();

        return Labels.Select((label, i) => (Label: label, Number: ordered[i])).ToDictionary(x => x.Label, x => x.Number, StringComparer.Ordinal);
    }

    private static string[] TasksArgs(Rig rig, params string[] extra) =>
    [
        "scouts", "tasks", "--candidates", rig.Candidates, "--cutoffs", rig.Cutoffs, "--opus-id", ScoutStreams.Opus, "--sonnet-id", ScoutStreams.Sonnet,
        "--out", rig.Setup.TasksFile, "--trees-root", rig.Setup.Trees, "--checkout-root", rig.Setup.Checkouts, "--repo-url-template", rig.Setup.Repo.Url,
        "--claude-settings", rig.Setup.SettingsFile, "--campaign", Campaign, "--campaign-key", rig.Key, "--private-tasks-file", rig.PrivateFile, .. extra,
    ];

    private static async Task<(int Code, string Output, string Error)> RunAsync(string[] args, string? faults = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = faults is null
            ? await ScoutsCommand.RunAsync(CommandLine.Parse(args), ScoutVerbServices.Machine, output, error, Ct)
            : Program.Run(args, output, error, Ct, faults);

        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task The_private_task_goes_through_every_verb_and_every_public_writer_without_one_planted_string_leaving()
    {
        await using var rig = await RigAsync(postgres);
        var setup = rig.Setup;
        var transcript = new List<string>();

        // 1. tasks — frozen, committed, written through the projection; the private detail to the local file.
        var tasks = await RunAsync(TasksArgs(rig));
        transcript.Add(tasks.Output + tasks.Error);
        tasks.Code.Should().Be(ExitCodes.Pass, tasks.Output + tasks.Error);
        tasks.Output.Should().Contain("frozen         p03 — private · PHP, |G| = 3").And.Contain("2 private, by label and commitment")
            .And.Contain("selected       main    p03 (private) → p03").And.Contain("selected       reserve p03 (private) → p01", "the repository is shown by its first candidate's label");
        var publicJson = File.ReadAllText(setup.TasksFile);
        var privateJson = File.ReadAllText(rig.PrivateFile);
        var committer = new ScoutCommitter(ScoutCampaignKey.Read(rig.Key).Ok(), Campaign);
        var task = ScoutFiles.Resolve(publicJson, privateJson, committer).Ok().Should().ContainSingle().Subject;
        (task.Privacy, task.Language).Should().Be((ScoutTaskPrivacy.Private, "PHP"));
        task.GroundTruth.Should().Equal(Truth);
        task.Removals.Should().Equal("todo/PLAN_kleebor.md");
        privateJson.Should().Contain(Statement).And.Contain("kleeborThrottleWindow").And.Contain(rig.Repo.Url, "the plants are alive in the local file");
        JsonNode.Parse(publicJson)!["tasks"]![0]!["groundTruthFileCommitments"]!.AsArray().Should().HaveCount(3);

        // 2. run — the fake claude replays a stream whose KEY FILES and CLAIMS carry the planted paths and claim.
        setup.Fake.Answer(ScoutPrompts.ForCell(task, ScoutArms.Of(ScoutArmId.A1)).Driver, ScoutCellStreams.For(task, ScoutArms.Of(ScoutArmId.A1), claim: Claim));
        var run = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--campaign-key", rig.Key, "--private-tasks-file", rig.PrivateFile]));
        transcript.Add(run.Output + run.Error);
        run.Code.Should().Be(ExitCodes.Pass, run.Output + run.Error);
        var runId = ScoutsCliSetup.RunIdOf(run.Output);
        (await setup.NewStore().LoadAsync(runId, Ct)).Ok().Campaign.Should().Be(Campaign);

        // 3. status · score --handcheck · assess · report · report --json
        var status = await setup.RunAsync(setup.Services(), "status", "--run", runId.ToString());
        transcript.Add(status.Output + status.Error);
        status.Code.Should().Be(ExitCodes.Pass, status.Error);
        var handcheck = Path.Combine(setup.Root.Path, "handcheck.md");
        var companion = Path.Combine(setup.Root.Sibling("private-files"), "handcheck.private.md");
        var scored = await setup.RunAsync(setup.Services(), "score", "--run", runId.ToString(), "--handcheck", "3", "--seed", "5", "--out", handcheck, "--private-out", companion, "--campaign-key", rig.Key);
        transcript.Add(scored.Output + scored.Error);
        scored.Code.Should().Be(ExitCodes.Pass, scored.Output + scored.Error);
        using var codex = new FakeCli(new JsonObject { ["assessVerdicts"] = new JsonObject { ["Kleebor.php"] = "wrong" } });
        var assessed = await setup.RunAsync(setup.Services(), "assess", "--run", runId.ToString(), "--assessor-model", codex.Model, "--codex-exe", FakeCli.Executable, "--campaign-key", rig.Key);
        transcript.Add(assessed.Output + assessed.Error);
        assessed.Code.Should().Be(ExitCodes.Pass, assessed.Output + assessed.Error);
        var again = await setup.RunAsync(setup.Services(), "score", "--run", runId.ToString(), "--handcheck", "3", "--seed", "5", "--out", handcheck, "--private-out", companion, "--campaign-key", rig.Key);
        transcript.Add(again.Output + again.Error);
        var text = await setup.RunAsync(setup.Services(), "report", "--run", runId.ToString());
        var json = await setup.RunAsync(setup.Services(), "report", "--run", runId.ToString(), "--json");
        transcript.Add(text.Output + text.Error + json.Output + json.Error);
        text.Code.Should().Be(ExitCodes.Pass, text.Error);
        text.Output.Should().Contain("task p03 (private · PHP, where-decided, |G| 3)");
        json.Output.Should().Contain("\"repository\":\"p03\"").And.Contain("\"privacy\":\"private\"").And.Contain("\"language\":\"PHP\"");

        // 4. the committed records, the export, the stored claim digest
        var reads = new PostgresScoutReads(PostgresFixture.Context(setup.Connection));
        var runRow = (await reads.RunAsync(runId, Ct)).Ok();
        var jsonl = ScoutRecordFiles.CellsJsonl(runRow, await reads.TasksAsync(runId, Ct), await reads.CellsAsync(runId, Ct), await reads.ScoutRecordsAsync(runId, Ct), await reads.VerdictsAsync(runId, Ct), ScrubDenyList.None).Ok();
        var skeleton = ScoutRecordFiles.ResultsSkeleton((await ScoutReport.ReadAsync(reads, runId, BootstrapSettings.Default, Ct)).Ok());
        var export = await ExportAsync(setup.Connection);
        var verdicts = await reads.VerdictsAsync(runId, Ct);
        verdicts.Should().HaveCount(2).And.Contain(v => v.StatementSha256 == committer.Claim("p03", Claim), "a private claim is stored as its HMAC")
            .And.NotContain(v => v.StatementSha256 == Bench.Domain.StableHash.Of(Claim), "never as its plain hash");
        jsonl.Should().Contain("\"task\":\"p03\"").And.Contain("\"keyFiles\":[]").And.Contain("\"keyFilesCount\":2");

        // 5. the public hand-check by label and commitment, the companion with the plaintext
        var handcheckText = File.ReadAllText(handcheck);
        handcheckText.Should().Contain("Task `p03` (private)").And.Contain(committer.GroundTruthFile("p03", "src/Zqvtharnish/Kleebor.php")).And.Contain(committer.Claim("p03", Claim));
        File.ReadAllText(companion).Should().Contain("src/Zqvtharnish/Kleebor.php").And.Contain(Claim);

        // 6. nothing planted anywhere public: every console line, the public files, the records, the export, the log sink
        foreach (var planted in Planted(rig))
        {
            string.Join('\n', transcript).Should().NotContain(planted, "no console line of any verb carries a private value");
            publicJson.Should().NotContain(planted, "tasks.json");
            handcheckText.Should().NotContain(planted, "the public hand-check");
            jsonl.Should().NotContain(planted, "cells.jsonl");
            skeleton.Should().NotContain(planted, "the RESULTS skeleton");
            export.Should().NotContain(planted, "the public export");
            LogText().Should().NotContain(planted, "the log sink");
        }
    }

    [Fact]
    public async Task Every_error_path_names_the_label_only_and_the_unhandled_exception_of_a_scouts_verb_is_withheld()
    {
        await using var rig = await RigAsync(postgres);
        var transcript = new List<string>();

        // malformed candidates: a private candidate whose parent is not a sha is refused by field, never by value
        File.WriteAllText(rig.Candidates, CandidatesJson("x", "y", rig.Repo.Url, parentOverride: "not-a-sha"));
        var malformed = await RunAsync(TasksArgs(rig));
        transcript.Add(malformed.Output + malformed.Error);
        malformed.Code.Should().Be(ExitCodes.Configuration, malformed.Error);
        malformed.Error.Should().Contain("candidate p03 (private): its parent is not a commit sha");

        // a missing mirror: the url is withheld, the label and the summary stay
        var missingUrl = new Uri(Path.Combine(rig.Setup.Root.Sibling("no-such-zqvtharnish-clone"))).AbsoluteUri;
        await using var missing = await RigAsync(postgres, missingUrl);
        var unreachable = await RunAsync(TasksArgs(missing));
        transcript.Add(unreachable.Output + unreachable.Error);
        unreachable.Code.Should().Be(ExitCodes.Environment, unreachable.Error);
        unreachable.Error.Should().Contain("task p03 (private): the mirror could not be read — the detail is withheld").And.NotContain("no-such-zqvtharnish-clone");
        File.ReadAllText(Path.Combine(missing.Setup.Trees, ".scout-trees", "p03.withheld.txt")).Should().Contain("no-such-zqvtharnish-clone", "the detail went to the local file");

        // the real task, frozen; then an I/O failure injected into its tree and an unhandled exception through the CLI entry
        File.WriteAllText(rig.Candidates, CandidatesJson(await rig.Repo.GitAsync("rev-parse", "HEAD~1").ContinueWith(t => t.Result.Trim(), TaskScheduler.Default), (await rig.Repo.GitAsync("rev-parse", "HEAD")).Trim(), rig.Repo.Url));
        var tasks = await RunAsync(TasksArgs(rig));
        transcript.Add(tasks.Output + tasks.Error);
        tasks.Code.Should().Be(ExitCodes.Pass, tasks.Output + tasks.Error);
        var locked = Path.Combine(rig.Setup.Trees, "p03", "src", "Zqvtharnish", "Kleebor.php");
        string[] runArgs = ["scouts", .. rig.Setup.WithDefaults(rig.Setup.RunArgs([ScoutArmId.A1], extra: ["--campaign-key", rig.Key, "--private-tasks-file", rig.PrivateFile]))];
        (int Code, string Output, string Error) faulted;
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            faulted = await RunAsync(runArgs, rig.Faults);
        }

        transcript.Add(faulted.Output + faulted.Error);
        faulted.Code.Should().Be(ExitCodes.Environment, faulted.Error);
        faulted.Error.Should().Contain("unhandled IOException in a scouts verb").And.Contain("withheld");
        Directory.EnumerateFiles(rig.Faults).Should().ContainSingle().Which.Should().Match(file => File.ReadAllText(file).Contains("Kleebor.php"), "the fault file holds the message");

        // an I/O failure injected into the artefact root: the empty MCP config's path made a directory
        Directory.CreateDirectory(Path.Combine(rig.Setup.ArtifactRoot, ScoutArtifacts.Folder, "empty-mcp.json"));
        var artefacts = await RunAsync(runArgs, rig.Faults);
        transcript.Add(artefacts.Output + artefacts.Error);
        artefacts.Code.Should().NotBe(ExitCodes.Pass, artefacts.Output + artefacts.Error);

        foreach (var planted in Planted(rig))
        {
            string.Join('\n', transcript).Should().NotContain(planted);
            LogText().Should().NotContain(planted);
        }
    }

    [Fact]
    public async Task The_key_is_created_once_outside_git_a_later_verb_without_it_refuses_and_creates_nothing_and_a_mismatch_is_refused_by_label()
    {
        await using var rig = await RigAsync(postgres);
        var setup = rig.Setup;
        using var checkout = await TempGitRepo.CreateAsync();

        // key init: refused inside a checkout, refused a second time at the same path
        var inside = await RunAsync(["scouts", "key", "init", "--campaign-key", Path.Combine(checkout.Path, "k.key"), "--trees-root", setup.Trees, "--checkout-root", setup.Checkouts, "--claude-settings", setup.SettingsFile]);
        inside.Code.Should().Be(ExitCodes.Configuration, inside.Error);
        inside.Error.Should().Contain("inside the git checkout");
        File.Exists(Path.Combine(checkout.Path, "k.key")).Should().BeFalse();
        var twice = await RunAsync(["scouts", "key", "init", "--campaign-key", rig.Key, "--trees-root", setup.Trees, "--checkout-root", setup.Checkouts, "--claude-settings", setup.SettingsFile]);
        twice.Code.Should().Be(ExitCodes.Configuration, twice.Error);
        twice.Error.Should().Contain("already exists");

        // tasks without a key: refused before any tree is built; with a missing key file: refused, nothing created
        var noKey = await RunAsync([.. TasksArgs(rig).Where(a => a != "--campaign-key" && a != rig.Key)]);
        noKey.Code.Should().Be(ExitCodes.Configuration, noKey.Error);
        noKey.Error.Should().Contain("2 drawn task(s) are private (p03, p01)").And.Contain(ScoutCampaignKey.Flag);
        Directory.Exists(Path.Combine(setup.Trees, "p03")).Should().BeFalse("nothing was built");
        var absent = Path.Combine(setup.Root.Sibling("keys"), "absent.key");
        var missingKey = await RunAsync([.. TasksArgs(rig).Select(a => a == rig.Key ? absent : a)]);
        missingKey.Code.Should().Be(ExitCodes.Configuration, missingKey.Error);
        missingKey.Error.Should().Contain("no campaign key at").And.Contain(ScoutCampaignKey.InitVerb);
        File.Exists(absent).Should().BeFalse("a verb never regenerates a key");

        // frozen with the key; then run without it (4, nothing created), with an edited statement (mismatch by label), with another key
        (await RunAsync(TasksArgs(rig))).Code.Should().Be(ExitCodes.Pass);
        var runWithout = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--private-tasks-file", rig.PrivateFile, "--campaign-key", absent]));
        runWithout.Code.Should().Be(ExitCodes.Configuration, runWithout.Output + runWithout.Error);
        runWithout.Error.Should().Contain("no campaign key at");
        File.Exists(absent).Should().BeFalse();
        (await setup.NewStore().CellsAsync(Guid.Empty, Ct)).Should().BeEmpty();

        var edited = JsonNode.Parse(File.ReadAllText(rig.PrivateFile))!;
        edited["tasks"]![0]!["statement"] = Statement + " Really?";
        var editedFile = Path.Combine(setup.Root.Sibling("private-files"), "edited.private.json");
        File.WriteAllText(editedFile, edited.ToJsonString());
        var mismatch = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--private-tasks-file", editedFile, "--campaign-key", rig.Key]));
        mismatch.Code.Should().Be(ExitCodes.Configuration, mismatch.Output + mismatch.Error);
        mismatch.Error.Should().Contain("task p03 (private): its public commitments are not the ones").And.NotContain("Vrembolix").And.NotContain("Really");

        var otherKey = Path.Combine(setup.Root.Sibling("keys"), "other.key");
        ScoutCampaignKey.Create(otherKey).Ok();
        var wrongKey = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--private-tasks-file", rig.PrivateFile, "--campaign-key", otherKey]));
        wrongKey.Code.Should().Be(ExitCodes.Configuration, wrongKey.Output + wrongKey.Error);
        wrongKey.Error.Should().Contain("task p03 (private): its public commitments are not the ones").And.NotContain("Vrembolix");

        // the public file alone cannot run a private task
        var publicOnly = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--campaign-key", rig.Key]));
        publicOnly.Code.Should().Be(ExitCodes.Configuration, publicOnly.Output + publicOnly.Error);
        publicOnly.Error.Should().Contain(ScoutFiles.PrivateTasksFlag);
        var insideGit = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--campaign-key", rig.Key, "--private-tasks-file", Path.Combine(checkout.Path, "tasks.private.json")]));
        insideGit.Code.Should().Be(ExitCodes.Configuration, insideGit.Output + insideGit.Error);
        insideGit.Error.Should().Contain("inside the git checkout");
    }

    private static async Task<string> ExportAsync(string connection)
    {
        await using var db = PostgresFixture.Context(connection);
        var source = new PostgresGatePublicationSource(db);

        return GatePublication.Export(await source.ReadAsync(Ct), Bench.Domain.Gate.PrivateNames.None.WithHosts([Environment.MachineName]), source.PublicUrlColumns, ScoutStoreFixtures.Noon).Ok();
    }

    /// <summary>Every line this process's log sink wrote so far — one file per run under the working directory's <c>logs/</c> (the sink flushes
    /// each event). Empty when no verb reached the logger yet.</summary>
    private static string LogText()
    {
        var logs = Path.Combine(Directory.GetCurrentDirectory(), BenchLogging.LogFolder);

        return Directory.Exists(logs)
            ? string.Join('\n', Directory.EnumerateFiles(logs, "*.log", SearchOption.AllDirectories).Select(Read))
            : string.Empty;
    }

    private static string Read(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
