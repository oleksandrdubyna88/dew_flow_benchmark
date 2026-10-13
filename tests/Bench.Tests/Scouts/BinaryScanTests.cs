using System.Text;
using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>Review finding (security, E2 code round): the hygiene scan skipped the CONTENT of a binary blob — a NUL in its first 8 KB — and
/// scanned its name only, so a deny-list entry inside a .pdb, a zip's directory, a compiled resource or a UTF-16 text file passed. The bytes
/// are scanned for every entry as UTF-8 and as UTF-16LE, ASCII letters case-insensitively; a non-ASCII character is matched as it is. The
/// first entry found (lowest index) is named, by index — never by value.</summary>
public sealed class BinaryScanTests
{
    private static readonly string[] Entries = ["contoso-internal", "Fabrikam", "Zürich"];

    [Fact]
    public void A_deny_list_entry_in_utf8_bytes_is_found_whatever_its_case_and_named_by_index_and_encoding()
    {
        byte[] blob = [0x00, 0x01, 0xFF, .. Encoding.UTF8.GetBytes("--CONTOSO-Internal--"), 0x00];

        var hit = BinaryScan.FirstIn(blob, Entries);

        hit.Should().Be(new BinaryHit(1, BinaryScan.Utf8));
        BinaryScan.FirstIn([0x4D, 0x5A, 0x00, .. Encoding.UTF8.GetBytes("fabrikam")], Entries).Should().Be(new BinaryHit(2, BinaryScan.Utf8));
    }

    [Fact]
    public void A_deny_list_entry_in_utf16le_bytes_is_found_too_and_a_non_ascii_entry_matches_as_it_is()
    {
        byte[] wide = [0x00, 0x00, .. Encoding.Unicode.GetBytes("x FABRIKAM y"), 0x00];
        byte[] accented = [0x00, .. Encoding.UTF8.GetBytes("in Zürich"), 0x00];
        byte[] accentedWide = [0x00, .. Encoding.Unicode.GetBytes("in Zürich"), 0x00];
        byte[] otherCase = [0x00, .. Encoding.UTF8.GetBytes("in ZÜRICH"), 0x00];

        BinaryScan.FirstIn(wide, Entries).Should().Be(new BinaryHit(2, BinaryScan.Utf16Le));
        BinaryScan.FirstIn(accented, Entries).Should().Be(new BinaryHit(3, BinaryScan.Utf8));
        BinaryScan.FirstIn(accentedWide, Entries).Should().Be(new BinaryHit(3, BinaryScan.Utf16Le));
        BinaryScan.FirstIn(otherCase, Entries).Should().Be(BinaryHit.None, "only ASCII letters fold — Ü is not ü to a byte scan, and the rule says so");
    }

    [Fact]
    public void A_clean_blob_an_empty_blob_and_an_empty_list_find_nothing_and_the_lowest_index_wins()
    {
        BinaryScan.FirstIn([0x00, 0x7F, 0x80, 0xFE, 0x41, 0x00], Entries).Should().Be(BinaryHit.None);
        BinaryScan.FirstIn([], Entries).Should().Be(BinaryHit.None);
        BinaryScan.FirstIn([0x00, .. Encoding.UTF8.GetBytes("fabrikam")], []).Should().Be(BinaryHit.None);
        BinaryScan.FirstIn([0x00, .. Encoding.UTF8.GetBytes("fabrikam and contoso-internal")], Entries).Should().Be(new BinaryHit(1, BinaryScan.Utf8), "the list's order, not the blob's");
        BinaryScan.FirstIn([0x00, .. Encoding.UTF8.GetBytes("contoso-interna")], Entries).Should().Be(BinaryHit.None, "a prefix is not the entry");
        BinaryScan.FirstIn([.. Encoding.UTF8.GetBytes("ccontoso-internal")], Entries).Should().Be(new BinaryHit(1, BinaryScan.Utf8), "a false start on the first byte is retried one byte on");
    }

    [Fact]
    public void The_scrubber_classifies_a_blob_by_a_nul_in_its_first_8_kb_and_names_a_binary_leftover_by_rule()
    {
        var deny = ScrubDenyList.Parse("contoso-internal\nFabrikam\n");
        byte[] text = Encoding.UTF8.GetBytes("plain text, no NUL");
        byte[] lateNul = [.. Encoding.UTF8.GetBytes(new string('x', ScoutScrubber.BinaryProbeBytes)), 0x00];
        byte[] binary = [0x00, .. Encoding.Unicode.GetBytes("fabrikam")];

        ScoutScrubber.IsBinary(text).Should().BeFalse();
        ScoutScrubber.IsBinary(lateNul).Should().BeFalse("a NUL past the probe window does not make a text file binary");
        ScoutScrubber.IsBinary(binary).Should().BeTrue();
        ScoutScrubber.BinaryLeftovers(binary, deny).Should().Equal("carries deny-list entry #2 in its bytes (UTF-16LE)");
        ScoutScrubber.BinaryLeftovers([0x00, 0x01], deny).Should().BeEmpty();
    }
}
