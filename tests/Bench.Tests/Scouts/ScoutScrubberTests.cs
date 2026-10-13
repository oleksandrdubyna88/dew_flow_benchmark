using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The public-repository hygiene of §3.13: the scrubber removes drive and home paths, e-mail addresses and every deny-list
/// entry from a scout text before it is committed; and a hygiene scan fails the suite when a committed scout fixture or record still
/// carries a home path, an e-mail or a deny-list entry.
/// <para>
/// The deny-list is the operator's: a LOCAL file named by <c>BENCH_SCOUT_DENYLIST</c>, read at run time, never committed. With the
/// variable unset the scan still checks home paths and e-mails, and says so in its reason.
/// </para></summary>
public sealed class ScoutScrubberTests
{
    /// <summary>The directories whose files are committed scout records: the stream fixtures here, and the calibration's research
    /// folder once E2 writes it.</summary>
    private static readonly string[] CommittedFolders =
    [
        Path.Combine("tests", "Bench.Tests", "Fixtures", "scouts"),
        Path.Combine("research", "scout-calibration"),
    ];

    [Fact]
    public void A_planted_home_path_an_email_and_a_deny_list_entry_are_scrubbed_and_the_rest_is_kept()
    {
        var deny = ScrubDenyList.Parse("# private names\nContoso\n\n  acme-internal  \n");
        const string text = @"Read C:\Users\someone\src\a.cs and /home/someone/b.cs; mail ops@contoso.example; Contoso's acme-internal repo; src/c.cs:12 — defines C";

        var scrubbed = ScoutScrubber.Scrub(text, deny);

        scrubbed.Should().NotContain("someone").And.NotContain("@").And.NotContainEquivalentOf("contoso").And.NotContain("acme-internal");
        scrubbed.Should().Contain("src/c.cs:12 — defines C", "a repository-relative claim is what the record is for");
        ScoutScrubber.Leftovers(scrubbed, deny).Should().BeEmpty();
        deny.Entries.Should().Equal("Contoso", "acme-internal");
    }

    [Fact]
    public void The_hygiene_check_names_every_kind_of_leftover_without_repeating_the_value()
    {
        var deny = ScrubDenyList.Parse("Contoso");

        ScoutScrubber.Leftovers(@"{""cwd"":""C:\\Users\\someone\\x""}", deny).Should().Equal(ScoutScrubber.HomeRule);
        ScoutScrubber.Leftovers("C:/x/AppData/Local/Temp", deny).Should().Equal(ScoutScrubber.HomeRule);
        ScoutScrubber.Leftovers("written by a.b@corp.example", deny).Should().Equal(ScoutScrubber.EmailRule);
        ScoutScrubber.Leftovers("the contoso checkout", deny).Should().Equal("carries deny-list entry #1");
        ScoutScrubber.Leftovers("C:/work/tree/src/a.cs and feature-dev@claude-plugins-official", deny)
            .Should().BeEmpty("the fixtures' neutral tree is a drive path by design, and a plugin id is not a mailbox");
    }

    /// <summary>The scan's planted negative: a fixture folder holding a file with a home path, an e-mail and a deny-list entry is found,
    /// file by file — so the scan below is known to be alive.</summary>
    [Fact]
    public void A_committed_record_carrying_a_planted_leftover_fails_the_hygiene_scan()
    {
        var folder = Directory.CreateTempSubdirectory("scout-hygiene-");

        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "clean.ndjson"), "{\"cwd\":\"C:\\\\work\\\\tree\"}\n");
            File.WriteAllText(Path.Combine(folder.FullName, "leaky.ndjson"), "{\"cwd\":\"/home/someone/tree\",\"by\":\"ops@contoso.example\",\"repo\":\"contoso-internal\"}\n");

            Scan(folder.FullName, ScrubDenyList.Parse("contoso-internal")).Should().Equal(
                $"leaky.ndjson: {ScoutScrubber.HomeRule}",
                $"leaky.ndjson: {ScoutScrubber.EmailRule}",
                "leaky.ndjson: carries deny-list entry #1");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Every_committed_scout_fixture_and_record_is_clean()
    {
        var root = Cli.Repository.Root;
        var deny = DenyList();
        var scanned = CommittedFolders.Select(f => Path.Combine(root, f)).Where(Directory.Exists).ToList();

        scanned.Should().Contain(Path.Combine(root, CommittedFolders[0]), "the scan must reach the fixtures it exists for — a scan of nothing passes forever");
        Directory.EnumerateFiles(scanned[0]).Should().HaveCountGreaterThanOrEqualTo(5);
        scanned.SelectMany(folder => Scan(folder, deny)).Should().BeEmpty(
            DenyListSet() ? "no committed scout record may carry a home path, an e-mail or a deny-list entry"
                          : "no committed scout record may carry a home path or an e-mail (BENCH_SCOUT_DENYLIST is unset, so the deny-list was not checked)");
    }

    private static IReadOnlyList<string> Scan(string folder, ScrubDenyList deny) =>
        [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .SelectMany(file => ScoutScrubber.Leftovers(File.ReadAllText(file), deny).Select(rule => $"{Path.GetRelativePath(folder, file)}: {rule}"))];

    private static bool DenyListSet() => Environment.GetEnvironmentVariable("BENCH_SCOUT_DENYLIST") is { Length: > 0 } path && File.Exists(path);

    private static ScrubDenyList DenyList() =>
        DenyListSet() ? ScrubDenyList.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("BENCH_SCOUT_DENYLIST")!)) : ScrubDenyList.None;
}
