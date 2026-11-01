using Bench.Cli;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The module's own folders under the root that something other than <c>init</c> must be able to make: today <c>&lt;root&gt;/pids</c>, which nothing creates before the first <c>run-arm</c> and which <c>lock</c> (an absent <c>unlisted</c> entry), <c>run-arm</c>'s admission, and later <c>run-all</c> and <c>chain</c> all need to stand on. ONE helper, so the rules are
/// one: the folder is made BY NAME and exclusively (libc <c>mkdir</c>, <see cref="ExclusiveFolder"/>), at mode 0700 set explicitly like <c>init</c>'s subfolders — and ONLY when this call made it: a folder that stands is judged and left exactly as it is, because under the isolation it is at 300 and a "make sure it is 0700" would unlock it.
/// A link there is refused (D17: <c>Directory.CreateDirectory</c> succeeds silently through a link to a folder, so the kind is judged), a regular file or another kind is refused, and a failure of the host is exit 3.</summary>
internal static class RootFolders
{
    private const UnixFileMode OwnerOnlyFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public static Passage<string> Pids(RootPaths root) => Pids(root, PlatformFactsReader.CurrentRoad);

    public static Passage<string> Pids(RootPaths root, PlatformRoad road) =>
        ExclusiveFolder.Make(root.Pids, road) switch
        {
            FolderMade.Made => Owned(root.Pids),
            FolderMade.AlreadyThere => Judged(root.Pids),
            FolderMade.Failed failed => Passage<string>.Stop(ExitCodes.Environment, $"{failed.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Passage<string> Owned(string pids)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(pids, OwnerOnlyFolder);
        }

        return new Passage<string>.Through(pids);
    }

    private static Passage<string> Judged(string pids) =>
        BoundedFile.IsLink(pids) ? Passage<string>.Stop(ExitCodes.Configuration, $"{pids} is a symbolic link — refused (D17)")
        : Directory.Exists(pids) ? new Passage<string>.Through(pids)
        : File.Exists(pids) ? Passage<string>.Stop(ExitCodes.Configuration, $"{pids} is a regular file, not a directory")
        : Passage<string>.Stop(ExitCodes.Configuration, $"{pids} is not a directory");
}
