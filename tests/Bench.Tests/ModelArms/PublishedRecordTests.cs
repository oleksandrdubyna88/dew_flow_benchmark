using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The experiment's published record never carries what holds the owner's key (plan <c>todo/PLAN_model_arms_E6.md</c> §5 E6.S3, E2 §8 item 14): <c>make</c> writes <c>mcp-coai.json</c> (its <c>env</c> may hold <c>COAI_CREDS_KEY</c>) and copies the panel's settings into <c>coai-data/</c> in each arm, and neither may reach <c>research/local-model-coding/data/</c>.
/// <para>
/// What this does NOT prove: that nothing else secret-shaped is published (it names the module's own two files and the settings copy's folder); a record kept anywhere but that folder.
/// </para></summary>
public sealed class PublishedRecordTests
{
    private static readonly string[] ForbiddenFiles = ["mcp-coai.json", "mcp-none.json"];
    private const string ForbiddenFolder = "coai-data";

    [Fact]
    public void The_published_record_holds_no_mcp_config_and_no_coai_data()
    {
        var record = Path.Combine(Cli.Repository.Root, "research", "local-model-coding", "data");
        var found = Scan(record);

        found.Files.Should().Contain(path => path.EndsWith("data/part6/arms/C1/objective.json", StringComparison.Ordinal), "the scan must still see a known file of the record, or an empty scan would pass");
        var named = found.Files.Where(path => ForbiddenFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal)).ToList();
        named.Should().BeEmpty("a file named {0} holds (or may hold) the owner's key and is never published", string.Join(" or ", ForbiddenFiles));
        found.Folders.Where(path => Path.GetFileName(path) == ForbiddenFolder).Should().BeEmpty("a folder named coai-data holds a copy of the panel's settings and is never published");
    }

    private static (List<string> Files, List<string> Folders) Scan(string root) =>
        ([.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/'))],
         [.. Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/'))]);
}
