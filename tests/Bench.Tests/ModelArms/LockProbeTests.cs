using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The gate lock's conflict code is a MEASUREMENT off Windows (E3.S0): the code a second open of a locked file throws is observed at construction, not read from a table written for two platforms. The pure half (<c>Decide</c>) takes what the two
/// opens threw — and what a THIRD open threw after the holder let go, the control that stops an <c>EIO</c> or a descriptor limit from being taken for the conflict — and the live half runs the probe in a scratch folder.
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
        var failure = LockProbe.Decide(Io("x", 5), released: Io("io", 5));

        failure.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("other than the lock").And.Contain("io");
        LockProbe.Decide(Io("x", 5), released: null).Should().Be(Outcome<int>.Success(5));
    }

    [Fact]
    public void A_second_open_that_succeeds_is_inert_and_any_other_fault_carries_its_words()
    {
        LockProbe.Decide(null, null).Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("inert");
        LockProbe.Decide(Io("x", 35), null).Should().Be(Outcome<int>.Success(35));
        LockProbe.Decide(new FileNotFoundException(), null).Should().BeOfType<Outcome<int>.Fail>("a subclass of IOException is not the conflict");
        LockProbe.Decide(new UnauthorizedAccessException("denied"), null).Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain("denied");
    }
}
