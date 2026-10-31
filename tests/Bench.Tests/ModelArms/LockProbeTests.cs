using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The gate lock's conflict code is a MEASUREMENT off Windows (E3.S0): the code a second open of a locked file throws is observed at construction, not read from a table written for two platforms. The pure half (<c>Decide</c>) takes what the two
/// opens threw — what a THIRD open threw after the holder let go (the control that stops an <c>EIO</c> from being taken for the conflict) and what the open of a DIFFERENT new file threw while the first was still held (the control that stops a descriptor limit) — and the live half runs the probe in a scratch folder.
/// <para>
/// What this does NOT prove: the code on macOS or FreeBSD (<c>EAGAIN</c> 35 by their headers; the probe observes it at the first run there — that IS the proof), or the probe on a file system whose <c>flock</c> is not supported (it then reports "inert" and no gate runs).
/// </para></summary>
public sealed class LockProbeTests
{
    private static IOException Io(string words, int hresult) => new(words) { HResult = hresult };

    [Fact]
    public void The_measured_conflict_code_is_the_runtimes_EWOULDBLOCK_on_linux()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "11 is Linux's EAGAIN; the probe measures whatever the host answers");
        using var temp = new TempFolder();

        LockProbe.Measure(temp.Path).Should().Be(Outcome<int>.Success(11));

        Directory.EnumerateFileSystemEntries(temp.Path).Should().BeEmpty("the probe file is gone after");
        FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Match(_ => true, _ => false).Should().BeTrue();
    }

    [Fact]
    public void A_conflict_that_outlives_the_holders_release_is_not_the_lock()
    {
        var failure = LockProbe.Decide(Io("x", 5), released: Io("io", 5), independent: null, independentAfter: null);

        failure.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("other than the lock").And.Contain("io");
        LockProbe.Decide(Io("x", 5), released: null, independent: null, independentAfter: null).Should().Be(Outcome<int>.Success(5));
    }

    [Fact]
    public void A_second_open_that_succeeds_is_inert_and_any_other_fault_carries_its_words()
    {
        LockProbe.Decide(null, null, null, null).Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("inert");
        LockProbe.Decide(Io("x", 35), null, null, null).Should().Be(Outcome<int>.Success(35));
        LockProbe.Decide(new FileNotFoundException(), null, null, null).Should().BeOfType<Outcome<int>.Fail>("a subclass of IOException is not the conflict");
        LockProbe.Decide(new UnauthorizedAccessException("denied"), null, null, null).Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("denied");
    }

    [Fact]
    public void A_host_that_cannot_open_a_second_file_while_the_first_is_held_is_out_of_descriptors_not_a_lock_conflict()
    {
        var exhausted = Io("Too many open files", 24);

        var answer = LockProbe.Decide(Io("x", 11), released: null, independent: exhausted, independentAfter: null);

        answer.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Be("the probe could not open a second file while holding the first (Too many open files) — descriptors exhausted?; no gate lock is attempted");
        LockProbe.Decide(null, null, exhausted, null).Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("descriptors exhausted", "an open that got through is not 'inert' either when no other file can be opened: the control comes first");
        LockProbe.Decide(Io("x", 11), released: null, independent: null, independentAfter: null).Should().Be(Outcome<int>.Success(11), "with the control through, the conflict code is the measurement it was");
    }

    [Fact]
    public void The_live_probe_opens_a_different_new_file_while_it_holds_the_first_and_leaves_nothing_behind()
    {
        using var temp = new TempFolder();
        var opened = new List<string>();
        var held = new List<bool>();

        var answer = LockProbe.Measure(temp.Path, _ => null, folder =>
        {
            opened.Add(folder);
            held.Add(Directory.EnumerateFileSystemEntries(folder).Any());
            return new IOException("Too many open files") { HResult = 24 };
        });

        opened.Should().Equal([temp.Path], "a control that fails ends the probe: the first control is the only one asked, of the probe's folder");
        held.Should().Equal([true], "it runs while the first file exists — and is held");
        answer.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("descriptors exhausted").And.Contain("Too many open files");
        Directory.EnumerateFileSystemEntries(temp.Path).Should().BeEmpty("the probe file is removed on this road too");
    }

    [Fact]
    public void The_real_control_creates_a_new_file_while_another_is_held_and_deletes_it()
    {
        using var temp = new TempFolder();
        using var held = new FileStream(temp.Combine("held"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

        LockProbe.IndependentOpen(temp.Path).Should().BeNull("a different new file opens while the first is held");

        Directory.EnumerateFileSystemEntries(temp.Path).Should().Equal([temp.Combine("held")], "the control's file is deleted after");
        LockProbe.IndependentOpen(temp.Combine("absent")).Should().BeOfType<DirectoryNotFoundException>("a control that cannot be made is reported with the runtime's own exception, not swallowed");
    }

    [Fact]
    public void A_control_that_fails_after_the_second_open_is_out_of_descriptors_too()
    {
        var answer = LockProbe.Decide(Io("x", 11), released: null, independent: null, independentAfter: Io("Too many open files", 24));

        answer.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("could not open a second file while holding the first (Too many open files)").And.Contain("descriptors exhausted");
    }

    [Fact]
    public void The_live_probe_runs_its_control_before_and_after_the_second_open_while_the_first_is_held()
    {
        using var temp = new TempFolder();
        var order = new List<string>();
        Exception? Second(string _)
        {
            order.Add("second");
            return order.Count(step => step == "second") == 1 ? Io("held", 11) : null;
        }

        var answer = LockProbe.Measure(temp.Path, Second, _ => { order.Add("control"); return null; });

        order.Should().Equal(["control", "second", "control", "second"], "the second open is bracketed by two controls while the first is held, and the released third open follows: a limit reached by the second open itself is seen by the control after it");
        answer.Should().Be(Outcome<int>.Success(11));
        var calls = 0;
        LockProbe.Measure(temp.Path, s => Io("held", 11), _ => ++calls == 2 ? Io("Too many open files", 24) : null).Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("descriptors exhausted", "a control that fails only AFTER the second open is read too");
        Directory.EnumerateFileSystemEntries(temp.Path).Should().BeEmpty();
    }

    [Fact]
    public void A_control_whose_create_failed_deletes_nothing_it_did_not_create()
    {
        using var temp = new TempFolder();
        var existing = temp.File("someone else's file", ".model-arms-lock-probe-control-x");

        LockProbe.Control(existing).Should().BeOfType<IOException>("CreateNew refuses a name that is taken");

        File.ReadAllText(existing).Should().Be("someone else's file", "a control that did not create the file does not remove it");
    }

    [Fact]
    public void A_folder_where_a_second_open_gets_through_is_named_as_the_one_where_locking_is_inert()
    {
        using var temp = new TempFolder();

        var answer = LockProbe.Measure(temp.Path, _ => null);

        answer.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain($"inert in {temp.Path}").And.Contain("no gate lock is attempted");
        Directory.EnumerateFileSystemEntries(temp.Path).Should().BeEmpty("the probe file is removed on this road too");
    }
}
