using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Leftovers;

/// <summary>
/// The pin of F31, the limit of the leftovers helper on Windows: where a process's NEW files and folders are owned by <c>BUILTIN\Administrators</c> (measured: pwsh on windows-latest), the kernel-owner key reads
/// the folder the helper made, and its claim, as a stranger's. The start-of-run sweep then does not remove what such a run left, and the audit does not name it; it waits for a person. Where a new file is the
/// CALLER's (an elevated Git Bash makes the caller's files, measured), the limit does not hold.
/// <para>Source: the check of <c>dew_flow_connect_other_ais</c> <c>src_mcp/tests/RunTempTests.cs</c> at <c>e060d77e</c>, <c>AnUntouchedClaim_StaysUnseen_OnAnElevatedWindowsRun</c> and its <c>OwnedByTheCaller</c> (coai #962),
/// WITHOUT its child run: there is no <c>OrphanClaimChild</c> here. This test makes a folder through the helper (<see cref="OwnedDir"/>) and asks whose it is.</para>
/// <para><b>The condition is who owns a new file, not elevation.</b> The question is asked of the folder's owner SID DIRECTLY (<see cref="OwnedByTheCaller"/>: <c>DirectoryInfo.GetAccessControl().GetOwner</c> against
/// <c>WindowsIdentity.GetCurrent().User</c>), never through <see cref="Scope.IsMine"/>: the helper is what F31 may change, and a skip decided by it would hide that change. When F31 changes the helper, the
/// assertions go red, and that is the purpose. Off Windows it skips, saying why; benchmark has no Windows leg, so it runs where the owner runs the suite on Windows.</para>
/// </summary>
public sealed class WindowsNewFileOwnerLimitTests : IDisposable
{
    private readonly OwnedScope _owned = DarwinLayout.FreshOwnedScope();

    public void Dispose() => _owned.Dispose();

    [Fact]
    public void AFolderTheHelperMakes_IsNotTheCallers_WhereNewFilesAreOwnedByAdministrators()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "pins the limit of a Windows process whose new files are owned by BUILTIN\\Administrators only: elsewhere the process owns the files it makes");
        var folder = _owned.Dir("f31-natural");
        Assert.SkipWhen(OperatingSystem.IsWindows() && OwnedByTheCaller(folder), "this process owns the files it makes (as an elevated Git Bash does), so the limit this pins does not hold here");

        Scope.IsMine(folder).Should().BeFalse("the helper's own folder is Administrators' on such a run, so the kernel-owner key reads it as a stranger's");
        File.Exists(folder + ".owner").Should().BeTrue("IsMine answers false on a missing file too, so the claim must be there for the line below to mean anything");
        Scope.IsMine(folder + ".owner").Should().BeFalse("and so is its claim");
    }

    /// <summary>The folder's owner SID is this process's user SID: read from the folder's security descriptor, independently of the helper under test.</summary>
    [SupportedOSPlatform("windows")]
    private static bool OwnedByTheCaller(string folder) =>
        new DirectoryInfo(folder).GetAccessControl().GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner
        && WindowsIdentity.GetCurrent().User is SecurityIdentifier me
        && owner.Equals(me);
}
