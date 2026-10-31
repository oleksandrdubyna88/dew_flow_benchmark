using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A fact read by the runtime cannot tell a regular file from a FIFO or a device, so on the portable road an untold kind satisfies "a regular file" (the road's printed line says the check was not made) and never satisfies "a directory" — a
/// directory IS told by the runtime. A kernel fact of untold kind satisfies nothing.
/// <para>
/// What this does NOT prove: any reading of a real file — only the decision over facts handed in (<c>PortableFactsTests</c> read live files).
/// </para></summary>
public sealed class FactsReadTests
{
    private static FactsRead.Read Untold(FactsSource source) =>
        new(new FileFacts("/r/experiment.json", null, UnixFileMode.UserRead) { Source = source, Identity = new FileIdentity(0, 0, FileKind.Unknown, 0m) });

    [Fact]
    public void A_portable_fact_of_untold_kind_satisfies_regular_and_not_directory()
    {
        var portable = Untold(FactsSource.Portable);

        portable.MustBe(FileKind.Regular).Should().BeSameAs(portable);
        portable.MustBe(FileKind.Directory).Should().BeOfType<FactsRead.Refused>().Which.Reason.Should().Contain("not a directory");
        Untold(FactsSource.Kernel).MustBe(FileKind.Regular).Should().BeOfType<FactsRead.Refused>("a kernel read that names no type is not a regular file");
    }
}
