using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The isolation snapshot's state (D11, E3 §8 item 2), read from its two immutable files as values: none, applying (the snapshot alone — a lock killed half-way), applied (both, with the same run and digest) or inconsistent. Every pair of the three states a
/// file can be read in is a row. <c>status</c>, <c>make</c> and <c>checks</c> print the one line <see cref="IsolationLine"/> spells; nothing here restores anything.
/// <para>
/// What this does NOT prove: that E5's writer produces these two shapes (E5.S1 owns the writer and its round-trip), or that the files are read by name (<c>StatusVerbTests</c>).
/// </para></summary>
public sealed class LockSnapshotTests
{
    private const string Snapshot = """{"run":"part7","digest":"d1","since":"2026-10-30T10:00:00Z","modes":[{"path":"/r","mode":"755"}]}""";
    private const string Applied = """{"run":"part7","digest":"d1","verifiedAt":"2026-10-30T10:00:05Z"}""";

    private static Read<string> Absent => new Read<string>.Absent();

    private static Read<string> Text(string text) => new Read<string>.Present(text);

    private static Read<string> Refused(string reason) => new Read<string>.Refused(reason);

    public static TheoryData<string, Read<string>, Read<string>, Type, string> Pairs => new()
    {
        { "neither file", Absent, Absent, typeof(LockState.None), "no isolation snapshot" },
        { "the snapshot alone is a lock killed half-way", Text(Snapshot), Absent, typeof(LockState.Applying), "part7" },
        { "both with one run and digest", Text(Snapshot), Text(Applied), typeof(LockState.Applied), "part7" },
        { "the applied file of another digest", Text(Snapshot), Text("""{"run":"part7","digest":"d2","verifiedAt":"2026-10-30T10:00:05Z"}"""), typeof(LockState.Inconsistent), "d1" },
        { "the applied file of another run", Text(Snapshot), Text("""{"run":"part6","digest":"d1","verifiedAt":"2026-10-30T10:00:05Z"}"""), typeof(LockState.Inconsistent), "part6" },
        { "the applied file alone", Absent, Text(Applied), typeof(LockState.Inconsistent), ".locked-modes.applied" },
        { "a snapshot that was refused", Refused(".locked-modes is a link"), Absent, typeof(LockState.Inconsistent), ".locked-modes is a link" },
        { "an applied file that was refused", Text(Snapshot), Refused(".locked-modes.applied is bigger than 1048576 bytes"), typeof(LockState.Inconsistent), "bigger than" },
        { "both refused", Refused("a is a link"), Refused("b is a link"), typeof(LockState.Inconsistent), "a is a link" },
        { "a snapshot refused beside an applied file", Refused(".locked-modes is a link"), Text(Applied), typeof(LockState.Inconsistent), ".locked-modes is a link" },
        { "an applied file refused with no snapshot", Absent, Refused(".locked-modes.applied is a link"), typeof(LockState.Inconsistent), ".locked-modes.applied is a link" },
        { "a snapshot that is not JSON", Text("not json"), Absent, typeof(LockState.Inconsistent), ".locked-modes" },
        { "a snapshot with no run", Text("""{"digest":"d1","since":"2026-10-30T10:00:00Z","modes":[]}"""), Absent, typeof(LockState.Inconsistent), "run" },
        { "a snapshot with no digest", Text("""{"run":"part7","since":"2026-10-30T10:00:00Z","modes":[]}"""), Absent, typeof(LockState.Inconsistent), "digest" },
        { "a snapshot with no since", Text("""{"run":"part7","digest":"d1","modes":[]}"""), Absent, typeof(LockState.Inconsistent), "since" },
        { "an applied file with no verifiedAt", Text(Snapshot), Text("""{"run":"part7","digest":"d1"}"""), typeof(LockState.Inconsistent), "verifiedAt" },
        { "an applied file that is not JSON", Text(Snapshot), Text("["), typeof(LockState.Inconsistent), ".locked-modes.applied" },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void The_state_is_none_applying_applied_or_inconsistent_by_the_two_files(string row, Read<string> snapshot, Read<string> applied, Type expected, string mentions)
    {
        var state = LockSnapshots.State(snapshot, applied);

        state.Should().BeOfType(expected, row);
        IsolationLine.Of(state).Should().Contain(mentions, row);
    }

    [Fact]
    public void The_applying_state_names_the_snapshots_time_and_the_applied_state_the_verification_time()
    {
        LockSnapshots.State(Text(Snapshot), Absent).Should().Be(new LockState.Applying("part7", "2026-10-30T10:00:00Z"));
        LockSnapshots.State(Text(Snapshot), Text(Applied)).Should().Be(new LockState.Applied("part7", "2026-10-30T10:00:05Z"), "it has been applied since the verification said so");
    }

    [Fact]
    public void Each_state_prints_the_one_line_every_verb_but_lock_and_unlock_shows()
    {
        IsolationLine.Of(new LockState.None()).Should().Be("no isolation snapshot");
        IsolationLine.Of(new LockState.Applied("part7", "2026-10-30T10:00:05Z")).Should().Be("isolation applied for part7 since 2026-10-30T10:00:05Z");
        IsolationLine.Of(new LockState.Applying("part7", "2026-10-30T10:00:00Z")).Should().Be("a lock was interrupted — run `lock part7` or `unlock`");
        IsolationLine.Of(new LockState.Inconsistent("the files disagree")).Should().Be("isolation snapshot inconsistent — the files disagree");
    }

    [Fact]
    public void A_run_name_with_a_control_character_is_printed_escaped_never_raw()
    {
        var snapshot = Text("""{"run":"a\nb","digest":"d1","since":"s","modes":[]}""");

        var line = IsolationLine.Of(LockSnapshots.State(snapshot, Absent));

        line.Should().NotContain("\n").And.Contain("a\\u000Ab");
    }

    [Fact]
    public void The_root_spells_the_applied_file_and_each_runs_markers_beside_the_snapshot()
    {
        var root = new RootPaths("/exp");

        root.LockedModesApplied.Should().Be(Path.Combine("/exp", ".locked-modes.applied"));
        root.RunMarker("part6", RunStage.Locked).Should().Be(Path.Combine("/exp", "chain", "part6", "locked"));
        root.RunMarker("part6", RunStage.Unlocked).Should().Be(Path.Combine("/exp", "chain", "part6", "unlocked"));
        root.RunMarker("part6", RunStage.ArmsDone).Should().Be(Path.Combine("/exp", "chain", "part6", "arms-done"));
        FluentActions.Invoking(() => root.RunMarker("../x", RunStage.Locked)).Should().Throw<ArgumentException>("a run is one path segment");
    }
}
