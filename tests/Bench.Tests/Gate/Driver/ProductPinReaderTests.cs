using Bench.Infrastructure.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.6 — <c>ProductPin.Read</c> over a real binary (the fake product) and a real checkout: the deployment set
/// hashed, <c>--version</c> read, and the dirty check scoped to the product's own project, untracked files excluded.</summary>
public sealed class ProductPinReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_binary_outside_any_checkout_has_an_empty_git_sha_and_says_so()
    {
        using var temp = GateStoreFixtures.NewRoot();
        var exe = CopyFake(temp.Path);

        var pin = (await new ProductPinReader().ReadAsync(exe, Ct)).Ok();

        pin.GitSha.Should().BeEmpty();
        pin.DirtyFiles.WasCaptured.Should().BeFalse("no checkout is not the same fact as a clean one");
        pin.Describe.Should().Contain("no checkout above the binary");
        pin.VersionText.Should().Be("connect-other-ais 0.39.0-fake");
        pin.BinarySha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Only_a_tracked_change_under_the_products_own_project_dirties_the_pin_and_the_pin_names_that_tree()
    {
        using var repo = new DatedGitRepo(Ct);
        await repo.InitAsync(("src/Product/Product.csproj", "<Project />"), ("init", "2026-09-01T10:00:00Z"));
        await repo.CommitManyAsync([("src/Product/Code.cs", "class A {}"), ("docs/Readme.md", "hello")], ("more", "2026-09-01T11:00:00Z"));
        var exe = CopyFake(Path.Combine(repo.Root, "src", "Product", "bin", "Debug", "net10.0"));
        var reader = new ProductPinReader();

        var clean = (await reader.ReadAsync(exe, Ct)).Ok();
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "src", "Product", "scratch.txt"), "notes", Ct);
        var withScratch = (await reader.ReadAsync(exe, Ct)).Ok();
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "docs", "Readme.md"), "changed elsewhere", Ct);
        var elsewhere = (await reader.ReadAsync(exe, Ct)).Ok();
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "src", "Product", "Code.cs"), "class B {}", Ct);
        var dirty = (await reader.ReadAsync(exe, Ct)).Ok();

        clean.GitSha.Should().NotBeEmpty();
        clean.CheckedTree.Should().Be("src/Product", "the dirty check is scoped to the project the binary was built from, and says so");
        clean.DirtyFiles.Value.Should().Be(0);
        withScratch.DirtyFiles.Value.Should().Be(0, "an untracked scratch file beside the source is not a changed product");
        elsewhere.DirtyFiles.Value.Should().Be(0, "a change elsewhere in the checkout is not the product's");
        dirty.DirtyFiles.Value.Should().Be(1, "a modified tracked file under the product's project is");
    }

    [Fact]
    public async Task A_rebuild_that_changed_only_a_dependency_moves_the_pin()
    {
        using var temp = GateStoreFixtures.NewRoot();
        var exe = CopyFake(temp.Path);
        var dependency = Path.Combine(temp.Path, "CoaiMcp.Core.dll");
        await File.WriteAllBytesAsync(dependency, [1, 2, 3], Ct);
        var before = await ProductPinReader.HashAsync(exe, Ct);

        await File.WriteAllBytesAsync(dependency, [1, 2, 4], Ct);

        (await ProductPinReader.HashAsync(exe, Ct)).Should().NotBe(before,
            "the apphost and the main dll did not change, and a pin over those two alone would call two products one");
    }

    [Fact]
    public async Task A_missing_binary_is_refused_by_name()
    {
        (await new ProductPinReader().ReadAsync(Path.Combine(Path.GetTempPath(), "no-such-coai-mcp.exe"), Ct)).Reason()
            .Should().Contain("does not exist").And.Contain("--coai-exe");
    }

    /// <summary>The fake's whole output folder, copied — an apphost beside its dll, the product's checkout-build shape.</summary>
    internal static string CopyFake(string destination)
    {
        var source = Path.GetDirectoryName(FakeCoai.Executable)!;
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        return Path.Combine(destination, Path.GetFileName(FakeCoai.Executable));
    }
}
