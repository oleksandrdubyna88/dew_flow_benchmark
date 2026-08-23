using Bench.Domain.Sessions;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Sessions.PhaseClassifierTests;

namespace Bench.Tests.Sessions;

/// <summary>The detectors, and the one they must NOT fire on.
/// <para>
/// The draft rule this replaces was "three or more reads after an execution phase is a loop". It is
/// reproduced below as <see cref="A_read_after_an_edit_is_healthy_verification_and_never_a_loop"/> so the
/// refuted version has a shape: a read after an edit is how a careful agent checks itself, and a detector
/// that punished it would push the corpus toward exactly the behaviour nobody wants.
/// </para></summary>
public sealed class SessionAnalysisTests
{
    private static readonly ToolTaxonomy Taxonomy = ToolTaxonomy.ClaudeCode;

    [Fact]
    public void A_read_after_an_edit_is_healthy_verification_and_never_a_loop()
    {
        // Read the file, edit it, read it back, build. The exact sequence the draft's rule called a
        // re-research loop, and the exact sequence a careful agent produces.
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Write, MutationEvidence.Changed, "src/Foo.cs", "Edit"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(4, ToolClass.Verify, MutationEvidence.Unchanged, "dotnet build", "Bash"));

        SessionAnalysis.Loops(calls).Should().BeEmpty(
            "the counter resets at every change to the tree — verifying an edit is not repeating a read");
    }

    [Fact]
    public void Three_reads_of_one_target_with_nothing_changed_between_them_is_a_loop()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Read, MutationEvidence.NotChecked, "src/Bar.cs"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(4, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"));

        var loops = SessionAnalysis.Loops(calls);

        loops.Should().ContainSingle();
        loops[0].Kind.Should().Be(FindingKind.ReResearchLoop);
        loops[0].Subject.Should().Be("src/Foo.cs");
        // The ordinals are the whole value of a finding: one a reader cannot go and check is an assertion.
        loops[0].Ordinals.Should().Equal([1, 3, 4]);
    }

    /// <summary>Found by a traced session, 2026-08-23. The window used to reset on any EXECUTION-phase
    /// call, and "execution phase" is not the same fact as "the tree changed": a shell command the
    /// allowlist conservatively counts as a write — <c>gh pr list</c> — carries phase Execution while its
    /// porcelain digest says Unchanged. A genuine loop straddling one of those was silently missed, which
    /// is a lost measurement in the flagship detector.</summary>
    [Fact]
    public void A_write_that_positively_changed_nothing_does_not_reset_the_window()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Write, MutationEvidence.Unchanged, "gh pr list", "Bash"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(4, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"));

        calls[1].Phase.Should().Be(SessionPhase.Execution, "the classification itself is not in question");

        var loops = SessionAnalysis.Loops(calls);

        loops.Should().ContainSingle();
        loops[0].Ordinals.Should().Equal([1, 3, 4]);
    }

    /// <summary>The mirror, and the reason the fix is not simply "reset only on Changed". A write whose
    /// tree reading never ran might have changed everything — git was unavailable, the workspace is not a
    /// repository, the budget was exceeded. Treating that as a no-op would MANUFACTURE a loop out of a gap
    /// in instrumentation, which is the same class of defect one direction over.</summary>
    [Fact]
    public void A_write_nobody_could_verify_still_resets_the_window()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Write, MutationEvidence.NotChecked, "src/Foo.cs", "Edit"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(4, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"));

        SessionAnalysis.Loops(calls).Should().BeEmpty();
    }

    /// <summary>A build changes no source, so it is not progress and must not reset the window either.</summary>
    [Fact]
    public void A_build_between_reads_does_not_reset_the_window()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Verify, MutationEvidence.Unchanged, "dotnet build", "Bash"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(4, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"));

        SessionAnalysis.Loops(calls).Should().ContainSingle();
    }

    [Fact]
    public void Two_reads_are_not_a_loop()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"));

        SessionAnalysis.Loops(calls).Should().BeEmpty();
    }

    [Fact]
    public void Three_searches_re_asking_one_question_are_a_chain()
    {
        var calls = Labelled(
            Search(1, "graph resolved edge"),
            Search(2, "resolved graph edge"),
            Search(3, "edge graph resolved"));

        var chains = SessionAnalysis.Chains(calls, Taxonomy);

        chains.Should().ContainSingle();
        chains[0].Kind.Should().Be(FindingKind.SearchVariantChain);
        chains[0].Ordinals.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Reading_three_files_from_one_folder_is_not_a_search_chain()
    {
        // Their targets share most of their words, which is exactly the trap: without the search predicate
        // the overlap test alone would call ordinary navigation a loop.
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Bench.Domain/Sessions/A.cs"),
            Call(2, ToolClass.Read, MutationEvidence.NotChecked, "src/Bench.Domain/Sessions/B.cs"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Bench.Domain/Sessions/C.cs"));

        SessionAnalysis.Chains(calls, Taxonomy).Should().BeEmpty();
    }

    [Fact]
    public void A_search_that_genuinely_moves_on_is_not_a_chain()
    {
        var calls = Labelled(
            Search(1, "phase classifier"),
            Search(2, "worktree porcelain digest"),
            Search(3, "vscode extension terminal"));

        SessionAnalysis.Chains(calls, Taxonomy).Should().BeEmpty();
    }

    [Fact]
    public void A_harmless_looking_call_that_moved_the_tree_is_reported_as_our_defect()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.Changed, "git stash", "Bash"));

        var found = SessionAnalysis.Disagreements(calls);

        found.Should().ContainSingle();
        found[0].Kind.Should().Be(FindingKind.MutationDisagreement);
    }

    [Fact]
    public void A_shell_command_counted_as_a_write_that_changed_nothing_becomes_an_allowlist_candidate()
    {
        var calls = Labelled(
            Call(1, ToolClass.Write, MutationEvidence.Unchanged, "gh pr list", "Bash"),
            Call(2, ToolClass.Write, MutationEvidence.Unchanged, "gh pr list", "Bash"));

        var found = SessionAnalysis.Candidates(calls, Taxonomy);

        found.Should().ContainSingle();
        found[0].Kind.Should().Be(FindingKind.AllowlistCandidate);
        found[0].Occurrences.Should().Be(2, "the allowlist's to-do list comes from measurement, not from a guess");
    }

    [Fact]
    public void An_edit_that_changed_the_tree_is_not_an_allowlist_candidate()
    {
        var calls = Labelled(Call(1, ToolClass.Write, MutationEvidence.Unchanged, "src/Foo.cs", "Edit"));

        SessionAnalysis.Candidates(calls, Taxonomy).Should().BeEmpty(
            "only SHELL commands are candidates — Edit is a write by definition, whatever the tree says");
    }

    [Fact]
    public void Phase_time_is_the_gap_until_the_next_call_and_a_backwards_clock_contributes_nothing()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "a", "Read", second: 10),
            Call(2, ToolClass.Read, MutationEvidence.NotChecked, "b", "Read", second: 40),
            // A resumed session whose clock went backwards. A negative span must not SUBTRACT from a total.
            Call(3, ToolClass.Write, MutationEvidence.Changed, "c", "Edit", second: 20));

        var research = SessionAnalysis.Phases(calls).Single(p => p.Phase == SessionPhase.Research);

        research.Elapsed.TotalSeconds.Should().Be(30, "10→40 counts, 40→20 does not");
    }

    [Fact]
    public void Unclassified_tools_are_counted_as_the_instruments_own_error_bar()
    {
        var economics = SessionAnalysis.Of(
            Labelled(Call(1, ToolClass.Unknown, MutationEvidence.NotChecked, "?", "SomeFutureTool")),
            Taxonomy);

        economics.UnknownTools.Should().Be(1,
            "a report that hid this would be quoting a denominator it had not stated");
    }

    [Fact]
    public void The_same_trace_always_yields_the_same_findings()
    {
        var calls = Labelled(
            Call(1, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(2, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Call(3, ToolClass.Read, MutationEvidence.NotChecked, "src/Foo.cs"),
            Search(4, "graph resolved"),
            Search(5, "resolved graph"),
            Search(6, "graph edge resolved"));

        var first = SessionAnalysis.Of(calls, Taxonomy);
        var second = SessionAnalysis.Of(calls, Taxonomy);

        second.Findings.Should().BeEquivalentTo(first.Findings,
            "the whole thesis is that a formula can do this — a detector with variance would be the "
            + "demonstration of the opposite");
    }

    private static IReadOnlyList<SessionToolCall> Labelled(params SessionToolCall[] calls) =>
        PhaseClassifier.Label(calls);

    private static SessionToolCall Search(int ordinal, string pattern) =>
        Call(ordinal, ToolClass.Read, MutationEvidence.NotChecked, pattern, "Grep");
}
