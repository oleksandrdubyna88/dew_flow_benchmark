using System.Security.AccessControl;
using System.Security.Principal;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The file-hash key: random, at least 32 bytes, owner-only from the instant it exists, created once — and a
/// root that has LOST its key while the database holds hashes is refused, never silently re-keyed.</summary>
public sealed class FileHashKeyFileTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_fresh_root_gets_a_random_32_byte_key_file_created_once()
    {
        using var temp = NewRoot();
        var store = Store(temp);

        (await store.ReadFileHashKeyAsync(Ct)).Should().BeOfType<FileHashKeyRead.Missing>();
        var created = (await store.CreateFileHashKeyAsync(Ct)).Ok();

        var path = Path.Combine(store.Root, FileHashKeyFile.FileName);
        var bytes = await File.ReadAllBytesAsync(path, Ct);
        bytes.Should().HaveCount(FileHashKeyFile.KeyBytes);
        bytes.Should().NotEqual(new byte[FileHashKeyFile.KeyBytes], "the bytes come from the system's generator, not a default");
        created.ToString().Should().Be("FileHashKey(redacted)", "the key never prints");
        (await store.CreateFileHashKeyAsync(Ct)).Reason().Should().Contain("already exists");
        ((FileHashKeyRead.Present)await store.ReadFileHashKeyAsync(Ct)).Key.Hash("src/A.cs").Should().Be(created.Hash("src/A.cs"));
    }

    [Fact]
    public async Task Two_roots_get_two_different_keys()
    {
        using var a = NewRoot();
        using var b = NewRoot();

        var first = (await Store(a).CreateFileHashKeyAsync(Ct)).Ok();
        var second = (await Store(b).CreateFileHashKeyAsync(Ct)).Ok();

        first.Hash("src/A.cs").Should().NotBe(second.Hash("src/A.cs"), "a key derived from anything guessable would make two roots agree");
    }

    [Fact]
    public async Task The_key_file_is_readable_by_its_owner_only()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        await store.CreateFileHashKeyAsync(Ct);
        var path = Path.Combine(store.Root, FileHashKeyFile.FileName);

        if (OperatingSystem.IsWindows())
        {
            var acl = new FileInfo(path).GetAccessControl();
            var rules = acl.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();

            acl.AreAccessRulesProtected.Should().BeTrue("nothing is inherited from the folder — a readable parent must not make the key readable");
            rules.Should().ContainSingle("one rule, the current user's").Which.IdentityReference.Should().Be(WindowsIdentity.GetCurrent().User);
        }
        else
        {
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite, "0600, set at creation");
        }
    }

    [Fact]
    public async Task A_short_key_file_is_unusable_and_says_so()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        await File.WriteAllBytesAsync(Path.Combine(store.Root, FileHashKeyFile.FileName), new byte[16], Ct);

        (await store.ReadFileHashKeyAsync(Ct)).Should().BeOfType<FileHashKeyRead.Unusable>().Which.Reason.Should().Contain("at least 32 bytes");
        (await GateFileHashKeys.ResolveAsync(store, new FindingsOnly(false), Ct)).Reason().Should().Contain("cannot be used");
    }

    [Fact]
    public async Task A_missing_key_is_created_when_nothing_was_hashed_yet()
    {
        using var temp = NewRoot();
        var store = Store(temp);

        (await GateFileHashKeys.ResolveAsync(store, new FindingsOnly(false), Ct)).Ok();

        File.Exists(Path.Combine(store.Root, FileHashKeyFile.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task A_missing_key_with_hashes_already_stored_is_refused_rather_than_regenerated()
    {
        using var temp = NewRoot();
        var store = Store(temp);

        (await GateFileHashKeys.ResolveAsync(store, new FindingsOnly(true), Ct)).Reason()
            .Should().Contain("already holds findings").And.Contain("restore");
        File.Exists(Path.Combine(store.Root, FileHashKeyFile.FileName)).Should().BeFalse("a silently regenerated key would make every stored file hash incomparable");
    }

    /// <summary>A store that answers one question — whether any finding exists — and refuses every other.</summary>
    private sealed class FindingsOnly(bool hasFindings) : ForwardingGateStore(null!)
    {
        public override Task<bool> HasFindingsAsync(CancellationToken cancellationToken) => Task.FromResult(hasFindings);
    }
}
