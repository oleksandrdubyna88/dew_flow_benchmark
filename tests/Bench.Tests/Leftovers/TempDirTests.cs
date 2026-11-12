using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Leftovers;

/// <summary>The scratch-folder entry of the later stories (E6 S2, brief § 10): <c>TempDir.For(what)</c> makes ONE owned, claimed folder and <c>Dispose</c> removes it with its claim.</summary>
public sealed class TempDirTests
{
    [Fact]
    public void For_MakesAnOwnedFolder_WithItsClaimBesideIt()
    {
        using var work = TempDir.For("tempdir-test");

        var name = Path.GetFileName(work.Path);
        name.Should().MatchRegex("^" + Regex.Escape(Owned.NameBase) + "tempdir-test-[A-Za-z0-9]{6}$");
        Directory.Exists(work.Path).Should().BeTrue();
        File.Exists(work.Path + ".owner").Should().BeTrue("the claim is made first and lies beside the folder");
        Path.GetDirectoryName(work.Path).Should().Be(TempDir.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    [Fact]
    public void Dispose_RemovesTheFolderItsContentsAndItsClaim()
    {
        var work = TempDir.For("tempdir-test");
        var inner = work.At("sub", "deeper");
        Directory.CreateDirectory(inner);
        File.WriteAllText(work.At("file.txt"), "x");
        File.WriteAllText(Path.Combine(inner, "file.txt"), "y");
        var path = work.Path;

        work.Dispose();

        Directory.Exists(path).Should().BeFalse("a file and a subfolder inside go with it");
        File.Exists(path + ".owner").Should().BeFalse("and so does the claim");
        work.Dispose(); // a second call is silence
    }

    [Fact]
    public void Root_IsTheTempRootOfTheHelper()
    {
        TempDir.Root.Should().Be(Owned.TempRoot());
    }

    [Fact]
    public void At_IsAPathUnderTheFolder_AndTheStringConversionIsItsPath()
    {
        using var work = TempDir.For("tempdir-test");

        work.At("a", "b").Should().Be(Path.Combine(work.Path, "a", "b"));
        string asString = work;
        asString.Should().Be(work.Path);
    }
}
