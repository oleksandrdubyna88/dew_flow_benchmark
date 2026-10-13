using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The campaign key's placement and lifecycle (plan §4 S4, §5 E2): refused inside a git checkout — also when reached THROUGH a
/// junction or link into one —, under the trees root, under the mirror root, or inside a directory the Claude user settings grant; created
/// once by <c>key init</c> and refused a second time; read back as the same bytes; a missing key is a refusal that creates nothing.</summary>
public sealed class ScoutCampaignKeyTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("scout-key-");

    public void Dispose()
    {
        foreach (var link in _root.EnumerateDirectories("*", SearchOption.AllDirectories).Where(d => d.LinkTarget is not null).ToList())
        {
            link.Delete();
        }

        TempGitRepo.DeleteTree(_root.FullName);
    }

    private string Trees => Path.Combine(_root.FullName, "trees");

    private string Mirrors => Path.Combine(_root.FullName, "mirrors");

    private string Settings => Path.Combine(_root.FullName, "claude", "settings.json");

    [Fact]
    public void A_key_outside_every_checkout_root_and_grant_is_accepted_and_created_once()
    {
        var path = Path.Combine(_root.FullName, "keys", "scouts.key");
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        File.WriteAllText(Settings, "{\"permissions\":{\"additionalDirectories\":[\"" + Path.Combine(_root.FullName, "granted").Replace('\\', '/') + "\"]}}");

        ScoutCampaignKey.Refusal(path, Trees, Mirrors, [Settings]).Should().BeEmpty();
        ScoutCampaignKey.Read(path).Reason().Should().Contain("no campaign key at").And.Contain(ScoutCampaignKey.InitVerb).And.Contain("never regenerates");
        File.Exists(path).Should().BeFalse("a read never creates a key");

        var created = ScoutCampaignKey.Create(path).Ok();
        var again = ScoutCampaignKey.Create(path).Reason();
        var read = ScoutCampaignKey.Read(path).Ok();

        again.Should().Contain("already exists").And.Contain("created once");
        new FileInfo(path).Length.Should().Be(FileHashKeyFile.KeyBytes);
        read.Hash("probe").Should().Be(created.Hash("probe"), "the file holds the bytes the committer was given");
        created.ToString().Should().Be("FileHashKey(redacted)");
        FileHashKeyFile.ReadFile(path).Should().BeOfType<Bench.Application.Gate.FileHashKeyRead.Present>();
    }

    [Fact]
    public async Task A_key_inside_a_git_checkout_is_refused_also_through_a_junction_into_one()
    {
        using var repo = await TempGitRepo.CreateAsync();
        var inside = Path.Combine(repo.Path, "keys", "scouts.key");
        var link = Path.Combine(_root.FullName, "elsewhere");
        DirectoryLink.CreateOrSkip(link, repo.Path);

        ScoutCampaignKey.Refusal(inside, Trees, Mirrors, []).Should().Contain("inside the git checkout").And.Contain("git add");
        ScoutCampaignKey.Refusal(Path.Combine(link, "keys", "scouts.key"), Trees, Mirrors, []).Should().Contain("inside the git checkout",
            "the junction is followed to the checkout it points into");
    }

    [Theory]
    [InlineData("trees", "under the trees root")]
    [InlineData("mirrors", "under the mirror root")]
    [InlineData("granted", "a directory the Claude user settings grant")]
    public void A_key_under_the_trees_root_the_mirror_root_or_a_granted_directory_is_refused(string folder, string rule)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        File.WriteAllText(Settings, "{\"permissions\":{\"additionalDirectories\":[\"" + Path.Combine(_root.FullName, "granted").Replace('\\', '/') + "\"]}}");

        ScoutCampaignKey.Refusal(Path.Combine(_root.FullName, folder, "deep", "scouts.key"), Trees, Mirrors, [Settings]).Should().Contain(rule).And.NotContain("FileHashKey");
    }

    [Fact]
    public void No_path_at_all_is_refused_naming_the_flag_and_the_variable()
    {
        ScoutCampaignKey.Refusal(string.Empty, Trees, Mirrors, []).Should().Contain(ScoutCampaignKey.Flag).And.Contain(ScoutCampaignKey.Variable);
        ScoutCampaignKey.PathOf("x.key").Should().Be("x.key");
    }
}
