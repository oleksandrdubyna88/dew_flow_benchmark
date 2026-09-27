using Bench.Application.Gate;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The claim owner — label, host, pid — is private sweep state. It is never a field of a public export row,
/// and a row that somehow carries one refuses the whole export, whatever its value.</summary>
public sealed class GatePublicationShapeTests
{
    [Theory]
    [InlineData("Owner")]
    [InlineData("OwnerHost")]
    [InlineData("OwnerPid")]
    public void A_claim_owner_field_in_any_export_row_is_refused_whatever_its_value(string column)
    {
        var value = column == "OwnerPid" ? (PublishedValue)new PublishedValue.Number(4242) : new PublishedValue.Text(string.Empty);
        var tables = new[] { new PublishedTable("gate_anything", [new PublishedRow("r1", [new PublishedField(column, value)])]) };

        GatePublication.Check(tables, PrivateNames.None, new HashSet<string>()).Select(v => v.Describe)
            .Should().Equal([$"gate_anything.{column} row r1: {GatePublication.ClaimOwnerRule}"],
                "the owner triple identifies a machine and a process; its absence is structural, and this is the line behind it");
    }
}
