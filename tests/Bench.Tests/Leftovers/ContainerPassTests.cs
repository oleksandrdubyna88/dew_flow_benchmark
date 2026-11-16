using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Leftovers;

/// <summary>
/// The container pass (E6 S3, scenarios (2) and (6)) against a FAKE <c>docker</c>: a Node script that records its argv and answers from a scenario file, started by its absolute
/// path (never found on <c>PATH</c>: changing <c>PATH</c> in-process would change it for every other test). The tests that need a daemon are in <see cref="FixtureContainerTests"/>.
/// </summary>
public sealed class ContainerPassTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(60);

    private const string ShebangReason = "the fake docker is a shebang script, which Windows does not run";

    [Fact]
    public void ThePass_removes_only_a_dead_owners_old_container_by_its_captured_id()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), ShebangReason);
        var now = DateTimeOffset.UtcNow;
        var dead = DeadRecord();
        var seven = new[]
        {
            Row(Id(1), Mine("pg-dead"), dead, now - TimeSpan.FromHours(1)),
            Row(Id(2), Mine("pg-live"), Owner.This.Format(), now - TimeSpan.FromHours(1)),
            Row(Id(3), AnotherOwner("pg-abc"), dead, now - TimeSpan.FromHours(1)),
            Row(Id(4), AnotherToken("pg-abc"), dead, now - TimeSpan.FromHours(1)),
            Row(Id(5), Mine("pg-young"), dead, now - TimeSpan.FromMinutes(1)),
            Row(Id(6), Mine("pg-bare"), "", now - TimeSpan.FromHours(1)),
            Row(Id(7), Legacy("pg-abc"), dead, now - TimeSpan.FromHours(1)),
        };
        using var dir = TempDir.For("fake-docker");
        var docker = FakeDocker.Write(dir, new Scenario(new PsPlan(Lines(seven)), seven.ToDictionary(r => r.Id, r => r.Line)));

        var result = ContainerPass.Run(Options(docker, now));

        var calls = FakeDocker.Calls(dir);
        calls.Should().HaveCount(3, "one ps, one inspect for every listed id, and one rm");
        calls[0].Should().Equal(ContainerPass.ListArgs, "the listing is by label, never by name");
        calls[1].Should().Equal(ContainerPass.InspectArgs(seven.Select(r => r.Id)));
        calls[2].Should().Equal(["rm", "-f", "-v", Id(1)], "the removal is by the captured id, with its anonymous volumes");
        calls.SelectMany(call => call).Should().NotContain(argument => argument.Contains("name="), "a name is a selector, never a filter that removes");
        result.Removed.Should().Equal([$"container {Mine("pg-dead")} ({Id(1)[..12]})"]);
        result.Preserved.Select(p => p.Reason).Should().Equal("live", "young", "unreadable record");
        result.Listed.Where(line => line.StartsWith("foreign:", StringComparison.Ordinal)).Should().HaveCount(2);
        result.Listed.Where(line => line.StartsWith("legacy:", StringComparison.Ordinal)).Should().ContainSingle();
        result.Errors.Should().BeEmpty();
    }

    public static TheoryData<string> JudgeCases()
    {
        var rows = new TheoryData<string>();
        foreach (var name in Cases().Keys) rows.Add(name);
        return rows;
    }

    [Theory]
    [MemberData(nameof(JudgeCases))]
    public void TheJudge_gives_each_container_its_verdict(string name)
    {
        var (row, expected) = Cases()[name];

        var verdict = ContainerPass.Judge(row, Options("docker", DateTimeOffset.UtcNow));

        Say(verdict).Should().Be(expected);
    }

    [Fact]
    public void TheRow_is_read_only_from_a_well_formed_inspect_line()
    {
        var name = Mine("pg-parse");
        var line = string.Join('\t', Id(9), Quote("/" + name), Quote("u1:h@linux:2"), "2026-11-16T10:11:12.123456789Z");

        ContainerRow.Parse(line).Should().Be(new ContainerRow(Id(9), name, "u1:h@linux:2", new DateTimeOffset(2026, 11, 16, 10, 11, 12, TimeSpan.Zero)), "a fraction of nine digits is cut to whole seconds");
        ContainerRow.Parse(line.Replace("12.123456789Z", "12Z", StringComparison.Ordinal))!.Created.Should().Be(new DateTimeOffset(2026, 11, 16, 10, 11, 12, TimeSpan.Zero));
        ContainerRow.Parse(string.Join('\t', Id(9), Quote("/" + name), Quote("x"))).Should().BeNull("three fields are no row");
        ContainerRow.Parse(line.Replace(Id(9), Id(9)[1..], StringComparison.Ordinal)).Should().BeNull("a 63-character id is no id");
        ContainerRow.Parse(line.Replace(Id(9), Id(9).Replace('9', 'A'), StringComparison.Ordinal)).Should().BeNull("an id is lower-case hex");
        ContainerRow.Parse(line.Replace(Quote("/" + name), Quote("/a\\\"b"), StringComparison.Ordinal))!.Name.Should().BeNull("an escaped name is not read");
        ContainerRow.Parse(line.Replace(Quote("/" + name), "/" + name, StringComparison.Ordinal))!.Name.Should().BeNull("a name that is not quoted is not read");
        ContainerRow.Parse(line.Replace(Quote("u1:h@linux:2"), "\"\"", StringComparison.Ordinal))!.Label.Should().BeEmpty("an empty label is read as empty, and is no record");
        ContainerRow.Parse(line.Replace("12.123456789Z", "12.123456789", StringComparison.Ordinal))!.Created.Should().BeNull("a creation time without its Z is not read");
    }

    [Fact]
    public void ThePass_inspects_in_batches_of_at_most_a_hundred()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), ShebangReason);
        var now = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(1, 101).Select(i => Row(Id(i), Mine("pg-y" + i), DeadRecord(), now - TimeSpan.FromMinutes(1))).ToArray();
        using var dir = TempDir.For("fake-docker");
        var docker = FakeDocker.Write(dir, new Scenario(new PsPlan(Lines(rows)), rows.ToDictionary(r => r.Id, r => r.Line)));

        var result = ContainerPass.Run(Options(docker, now));

        var calls = FakeDocker.Calls(dir);
        calls.Select(call => call[0]).Should().Equal("ps", "inspect", "inspect");
        calls[1].Skip(InspectPrefix).Should().HaveCount(100);
        calls[2].Skip(InspectPrefix).Should().HaveCount(1);
        result.Preserved.Should().HaveCount(101).And.OnlyContain(p => p.Reason == "young");
        result.Removed.Should().BeEmpty("nothing is old enough to remove");
    }

    [Fact]
    public void ThePass_calls_nothing_but_ps_when_ps_lists_nothing()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), ShebangReason);
        using var dir = TempDir.For("fake-docker");
        var docker = FakeDocker.Write(dir, new Scenario(new PsPlan(""), []));

        var result = ContainerPass.Run(Options(docker, DateTimeOffset.UtcNow));

        FakeDocker.Calls(dir).Should().ContainSingle().Which.Should().Equal(ContainerPass.ListArgs);
        result.Count.Should().Be(0);
    }

    public static TheoryData<string> SoftFailures() => new() { "ps_exit", "ps_timeout", "inspect_exit", "rm_exit", "an_exception", "a_row_that_cannot_be_read" };

    [Theory]
    [MemberData(nameof(SoftFailures))]
    public void ThePass_fails_softly(string failure)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), ShebangReason);
        var now = DateTimeOffset.UtcNow;
        var old = now - TimeSpan.FromHours(1);
        var deadRows = new[] { Row(Id(1), Mine("pg-one"), DeadRecord(), old), Row(Id(2), Mine("pg-two"), DeadRecord(), old) };
        var rows = deadRows.ToDictionary(r => r.Id, r => r.Line);
        using var dir = TempDir.For("fake-docker");
        var (scenario, options) = failure switch
        {
            "ps_exit" => (new Scenario(new PsPlan("", "boom\nsecond line", 3), rows), Options("", now)),
            "ps_timeout" => (new Scenario(new PsPlan("", "", 0, 30_000), rows), Options("", now) with { CallTimeout = TimeSpan.FromSeconds(2) }),
            "inspect_exit" => (new Scenario(new PsPlan(Lines(deadRows)), rows, InspectExit: 1), Options("", now)),
            "rm_exit" => (new Scenario(new PsPlan(Lines(deadRows)), rows, RmExit: new() { [Id(1)] = 1 }), Options("", now)),
            "an_exception" => (new Scenario(new PsPlan(Lines(deadRows)), rows), Options("", now) with { Consider = _ => throw new InvalidOperationException("planted\nsecond line") }),
            _ => (new Scenario(new PsPlan(Lines(deadRows)), new Dictionary<string, string> { [Id(1)] = "not four fields", [Id(2)] = deadRows[1].Line }), Options("", now)),
        };
        var docker = FakeDocker.Write(dir, scenario);

        var clock = Stopwatch.StartNew();
        var result = ContainerPass.Run(options with { Docker = docker });

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "a call that hangs is cut at its timeout, and the pass returns");
        var calls = FakeDocker.Calls(dir).Select(call => call[0]).ToList();
        switch (failure)
        {
            case "ps_exit":
                result.Errors.Should().Equal("containers: docker ps failed (exit 3): boom");
                calls.Should().Equal("ps");
                break;
            case "ps_timeout":
                result.Errors.Should().Equal("containers: docker ps timed out after 2s — nothing removed");
                break;
            case "inspect_exit":
                result.Errors.Should().Equal("containers: docker inspect failed (exit 1): Error: inspect refused — nothing removed");
                calls.Should().Equal("ps", "inspect").And.NotContain("rm");
                break;
            case "rm_exit":
                result.Errors.Should().Equal($"containers: docker rm {Id(1)[..12]} failed (exit 1): Error: cannot remove {Id(1)}");
                result.Removed.Should().Equal([$"container {Mine("pg-two")} ({Id(2)[..12]})"], "the next row goes on");
                calls.Should().Equal("ps", "inspect", "rm", "rm");
                break;
            case "an_exception":
                result.Errors.Should().Equal("containers: failed: planted");
                break;
            default:
                result.Listed.Should().Contain("notice: docker inspect printed a line that cannot be read, ignored").And.Contain($"notice: container {Id(1)[..12]} — docker inspect returned no row for it, ignored");
                result.Removed.Should().Equal([$"container {Mine("pg-two")} ({Id(2)[..12]})"]);
                break;
        }
    }

    [Fact]
    public void ThePass_is_one_notice_line_when_there_is_no_docker()
    {
        using var dir = TempDir.For("no-docker");

        var result = ContainerPass.Run(Options(dir.At("docker"), DateTimeOffset.UtcNow));

        result.Listed.Should().ContainSingle().Which.Should().StartWith("notice: containers not swept — ");
        result.Errors.Should().BeEmpty();
        result.Removed.Should().BeEmpty();
        result.Preserved.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheReaper_check_refuses_exactly_when_the_reaper_is_off(bool enabled, bool refuses)
    {
        var act = () => ContainerPass.RequireReaper(enabled);

        if (refuses) act.Should().Throw<InvalidOperationException>().WithMessage("The Testcontainers reaper (Ryuk) is switched off here*rule §5 keeps it on*");
        else act.Should().NotThrow();
    }

    [Fact]
    public void TheLabel_key_and_the_template_name_the_owner_label_the_fixture_writes()
    {
        ContainerPass.LabelKey.Should().Be("testsToDelete.owner");
        ContainerPass.Template.Should().Contain("index .Config.Labels \"testsToDelete.owner\"").And.Contain("\t");
        ContainerPass.RemoveArgs("x").Should().Equal("rm", "-f", "-v", "x");
    }

    private const int InspectPrefix = 5;

    private static ContainerPassOptions Options(string docker, DateTimeOffset now) =>
        new(docker, now, LeftoversSweep.MinAge, Liveness.ProbeProcess, Generous);

    private static string Id(int n) => n.ToString("x", CultureInfo.InvariantCulture).PadLeft(64, '0');

    private static string Mine(string what) => Owned.NameBase + what + "-abc123";

    private static string AnotherOwner(string what) => Owned.Prefix + (OwnerToken.This == "u4294967295" ? "u4294967294" : "u4294967295") + "-" + OwnedToken.RepoToken + "-" + what + "-abc123";

    private static string AnotherToken(string what) => Owned.Prefix + OwnerToken.This + "-another-" + what;

    private static string Legacy(string what) => Owned.Prefix + OwnedToken.RepoToken + "-" + what;

    private static string DeadRecord()
    {
        var pid = UnmatchablePid.Next();
        UnmatchablePid.AssertNoSuchProcess(pid);
        return (Owner.This with { Pid = pid }).Format();
    }

    private static string Quote(string text) => "\"" + text + "\"";

    private sealed record InspectRow(string Id, string Line);

    private static InspectRow Row(string id, string name, string label, DateTimeOffset created) =>
        new(id, string.Join('\t', id, Quote("/" + name), Quote(label), created.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "00Z"));

    private static string Lines(IEnumerable<InspectRow> rows) => string.Concat(rows.Select(r => r.Id + "\n"));

    private static string Say(ContainerVerdict verdict) => verdict switch
    {
        ContainerVerdict.Remove => "remove",
        ContainerVerdict.Keep keep => "keep: " + keep.Reason,
        ContainerVerdict.List listed => "list: " + listed.Line,
        _ => "unknown verdict",
    };

    private static Dictionary<string, (ContainerRow Row, string Expected)> Cases()
    {
        var now = DateTimeOffset.UtcNow;
        var old = now - TimeSpan.FromHours(1);
        var id = Id(0xabc);
        var short12 = id[..12];
        var dead = DeadRecord();
        ContainerRow With(string? name, string? label, DateTimeOffset? created) => new(id, name, label, created);
        return new Dictionary<string, (ContainerRow, string)>
        {
            ["name_not_readable"] = (With(null, dead, old), $"list: notice: container {short12} — its name is not readable, ignored"),
            ["not_an_owner_form_name"] = (With("postgres_db", dead, old), $"list: foreign: container postgres_db ({short12}) — not a bench owner-form name"),
            ["legacy_form"] = (With(Legacy("pg-x"), dead, old), $"list: legacy: container {Legacy("pg-x")} ({short12}) — the old form is listed, never collected; after checking: docker rm -f -v {id}"),
            ["another_owner"] = (With(AnotherOwner("pg"), dead, old), $"list: foreign: container {AnotherOwner("pg")} ({short12}) — another owner's or another repository's"),
            ["another_token"] = (With(AnotherToken("pg"), dead, old), $"list: foreign: container {AnotherToken("pg")} ({short12}) — another owner's or another repository's"),
            ["unknown_age"] = (With(Mine("pg"), dead, null), "keep: unknown age"),
            ["young"] = (With(Mine("pg"), dead, now - TimeSpan.FromMinutes(9)), "keep: young"),
            ["unreadable_record_null"] = (With(Mine("pg"), null, old), "keep: unreadable record"),
            ["unreadable_record_text"] = (With(Mine("pg"), "not a record", old), "keep: unreadable record"),
            ["live"] = (With(Mine("pg"), Owner.This.Format(), old), "keep: live"),
            ["unknown"] = (With(Mine("pg"), (Owner.This with { Host = "another-host@linux" }).Format(), old), "keep: unknown"),
            ["gone"] = (With(Mine("pg"), dead, old), "remove"),
        };
    }

    private sealed record PsPlan(string Stdout, string Stderr = "", int Exit = 0, int SleepMs = 0);

    private sealed record Scenario(PsPlan Ps, Dictionary<string, string> Rows, int InspectExit = 0, Dictionary<string, int>? RmExit = null);

    /// <summary>The fake <c>docker</c>: a Node script in the test's own folder, with its scenario beside it. It never reads the temp variables and spells no temp path.</summary>
    private static class FakeDocker
    {
        private const string Script = """
            #!/usr/bin/env node
            const fs = require('fs');
            const path = require('path');
            const here = path.dirname(process.argv[1]);
            const argv = process.argv.slice(2);
            fs.appendFileSync(path.join(here, 'calls.jsonl'), JSON.stringify(argv) + '\n');
            const plan = JSON.parse(fs.readFileSync(path.join(here, 'scenario.json'), 'utf8'));
            function finish(stdout, stderr, code, sleepMs) {
              setTimeout(() => process.stdout.write(stdout, () => process.stderr.write(stderr, () => process.exit(code))), sleepMs || 0);
            }
            const verb = argv[0];
            if (verb === 'ps') {
              const ps = plan.ps || {};
              finish(ps.stdout || '', ps.stderr || '', ps.exit || 0, ps.sleepMs);
            } else if (verb === 'inspect') {
              if (plan.inspectExit) {
                finish('', 'Error: inspect refused\n', plan.inspectExit, 0);
              } else {
                const known = argv.slice(5).filter((id) => id in plan.rows);
                const missing = argv.slice(5).filter((id) => !(id in plan.rows));
                finish(known.map((id) => plan.rows[id] + '\n').join(''), missing.map((id) => 'Error: No such object: ' + id + '\n').join(''), missing.length ? 1 : 0, 0);
              }
            } else if (verb === 'rm') {
              const id = argv[argv.length - 1];
              const code = (plan.rmExit || {})[id] || 0;
              finish('', code ? 'Error: cannot remove ' + id + '\n' : '', code, 0);
            } else {
              finish('', 'fake docker: unexpected ' + verb + '\n', 2, 0);
            }
            """;

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static string Write(TempDir dir, object scenario)
        {
            var docker = dir.At("docker");
            File.WriteAllText(dir.At("scenario.json"), JsonSerializer.Serialize(scenario, Json));
            File.WriteAllText(docker, Script.ReplaceLineEndings("\n") + "\n");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(docker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return docker;
        }

        public static IReadOnlyList<string[]> Calls(TempDir dir)
        {
            var file = dir.At("calls.jsonl");
            return File.Exists(file) ? [.. File.ReadAllLines(file).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<string[]>(l)!)] : [];
        }
    }
}
