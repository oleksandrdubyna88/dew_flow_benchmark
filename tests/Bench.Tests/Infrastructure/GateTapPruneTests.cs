using System.Text;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary><c>bench gate prune</c>'s store half: tap BODIES past the window go, FACTS stay; an unfinished attempt is
/// listed and never touched; and a prune killed half-way leaves the facts plus some bodies — never neither.</summary>
public sealed class GateTapPruneTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Cutoff = Now.AddDays(-30);

    [Fact]
    public async Task Bodies_past_the_window_are_released_and_every_facts_file_stays()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 2, recorded: true, ageDays: 40);

        var report = await store.PruneTapAsync(Cutoff, dryRun: false, Ct);

        report.Entries.Should().ContainSingle().Which.State.Should().Be(TapPruneState.Released);
        report.BodiesReleased.Should().Be(4);
        Directory.EnumerateFiles(attempt.Tap).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .Should().Equal("call-01.json", "call-02.json", TapPruner.PrunedLog);
        (await File.ReadAllLinesAsync(Path.Combine(attempt.Tap, TapPruner.PrunedLog), Ct)).Should().HaveCount(4)
            .And.OnlyContain(line => line.Contains("\"sha256\":", StringComparison.Ordinal), "what went is recorded with its hash and length");
    }

    [Fact]
    public async Task Bodies_inside_the_window_stay()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 1, recorded: true, ageDays: 3);

        var report = await store.PruneTapAsync(Cutoff, dryRun: false, Ct);

        report.Entries.Should().ContainSingle().Which.State.Should().Be(TapPruneState.Recent);
        Directory.EnumerateFiles(attempt.Tap).Should().HaveCount(3);
    }

    [Fact]
    public async Task A_dry_run_lists_what_would_go_and_deletes_nothing()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 2, recorded: true, ageDays: 40);

        var report = await store.PruneTapAsync(Cutoff, dryRun: true, Ct);

        report.Entries.Should().ContainSingle().Which.Should().Match<TapPruneEntry>(e => e.State == TapPruneState.WouldRelease && e.Bodies == 4);
        report.BodiesReleased.Should().Be(0);
        Directory.EnumerateFiles(attempt.Tap).Should().HaveCount(6, "a dry run touches nothing");
    }

    [Fact]
    public async Task An_attempt_without_a_run_record_is_listed_and_not_touched()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 2, recorded: false, ageDays: 400);

        var report = await store.PruneTapAsync(Cutoff, dryRun: false, Ct);

        report.Entries.Should().ContainSingle().Which.State.Should().Be(TapPruneState.Unfinished);
        Directory.EnumerateFiles(attempt.Tap).Should().HaveCount(6, "a session that never finished is evidence nobody has read yet");
    }

    [Fact]
    public async Task An_interrupted_attempt_is_kept_whole()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 1, recorded: true, ageDays: 400);
        (await store.BeginAttemptAsync(attempt.Scope with { Attempt = 2 }, Ct)).Ok();

        var report = await store.PruneTapAsync(Cutoff, dryRun: false, Ct);

        report.Entries.Select(e => (e.Attempt, e.State)).Should().Equal((1, TapPruneState.Interrupted), (2, TapPruneState.Unfinished));
        Directory.EnumerateFiles(attempt.Tap).Should().HaveCount(3);
    }

    [Fact]
    public async Task A_call_with_bodies_and_no_facts_file_keeps_its_bodies()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 1, recorded: true, ageDays: 40);
        File.Delete(Path.Combine(attempt.Tap, "call-01.json"));

        var report = await store.PruneTapAsync(Cutoff, dryRun: false, Ct);

        report.Entries.Should().ContainSingle().Which.State.Should().Be(TapPruneState.FactsMissing);
        Directory.EnumerateFiles(attempt.Tap).Should().HaveCount(2, "deleting the bodies would leave nothing at all about that call");
    }

    [Fact]
    public async Task A_link_planted_at_an_attempts_tap_folder_is_never_followed()
    {
        using var temp = NewRoot();
        var store = Store(temp, clock: new TestClock(Now));
        var attempt = await AttemptAsync(store, calls: 1, recorded: true, ageDays: 40);
        var outside = temp.Sibling("outside");
        Directory.CreateDirectory(outside);
        foreach (var file in Directory.EnumerateFiles(attempt.Tap))
        {
            File.Move(file, Path.Combine(outside, Path.GetFileName(file)));
        }

        Directory.Delete(attempt.Tap);
        DirectoryLink.CreateOrSkip(attempt.Tap, outside);

        var report = await store.PruneTapAsync(Cutoff, dryRun: false, Ct);

        report.Entries.Should().ContainSingle().Which.State.Should().Be(TapPruneState.Linked);
        Directory.EnumerateFiles(outside).Should().HaveCount(3, "a prune deletes, so a junction at tap/ must not point it at somebody else's files");
    }

    [Fact]
    public async Task A_prune_killed_half_way_leaves_the_facts_and_some_bodies_and_the_next_prune_finishes()
    {
        using var temp = NewRoot();
        var attempt = await AttemptAsync(Store(temp, clock: new TestClock(Now)), calls: 2, recorded: true, ageDays: 40);
        var dying = Store(temp, new CrashAt(ArtifactStep.BodyDeleted), new TestClock(Now));

        await ((Func<Task>)(() => dying.PruneTapAsync(Cutoff, dryRun: false, Ct))).Should().ThrowAsync<SimulatedCrash>();

        var left = Directory.EnumerateFiles(attempt.Tap).Select(Path.GetFileName).ToList();
        left.Should().Contain(["call-01.json", "call-02.json"], "the facts were made durable before any body went");
        left.Count(f => f!.EndsWith(".request.json", StringComparison.Ordinal) || f.EndsWith(".response.json", StringComparison.Ordinal))
            .Should().Be(3, "one body went before the crash — some bodies remain, never neither");
        (await File.ReadAllTextAsync(Path.Combine(attempt.Tap, TapPruner.PrunedLog), Ct)).Should().Contain("call-01.request.json",
            "the release was logged durably BEFORE the body went — a body deleted with no record is a hole nobody can explain");

        (await Store(temp, clock: new TestClock(Now)).PruneTapAsync(Cutoff, dryRun: false, Ct)).BodiesReleased.Should().Be(3);
        Directory.EnumerateFiles(attempt.Tap).Select(Path.GetFileName).Should().BeEquivalentTo(["call-01.json", "call-02.json", TapPruner.PrunedLog]);
    }

    private static async Task<(ArtifactScope Scope, string Tap)> AttemptAsync(FileSystemGateArtifactStore store, int calls, bool recorded, int ageDays)
    {
        var scope = new ArtifactScope(Run(), Guid.CreateVersion7(), Attempt: 1);
        (await store.BeginAttemptAsync(scope, Ct)).Ok();

        for (var call = 1; call <= calls; call++)
        {
            foreach (var (name, kind) in new[] { ("request.json", ArtifactClass.TapRequest), ("response.json", ArtifactClass.TapResponse), (string.Empty, ArtifactClass.TapFacts) })
            {
                var file = name.Length == 0 ? $"tap/call-{call:00}.json" : $"tap/call-{call:00}.{name}";
                (await store.WriteAsync(scope, kind, CellPaths.AttemptRoot(scope).Then(file).Ok(), Encoding.UTF8.GetBytes($"{{\"call\":{call}}}"), Ct)).Ok();
            }
        }

        if (recorded)
        {
            (await store.WriteAsync(scope, ArtifactClass.RunRecord, CellPaths.AttemptRoot(scope).Then(CellPaths.RunRecordFile).Ok(), "{}"u8.ToArray(), Ct)).Ok();
        }

        var tap = Path.Combine([store.Root, .. CellPaths.AttemptRoot(scope).Segments, CellPaths.TapFolder]);

        foreach (var file in Directory.EnumerateFiles(tap))
        {
            File.SetLastWriteTimeUtc(file, Now.AddDays(-ageDays).UtcDateTime);
        }

        return (scope, tap);
    }
}
