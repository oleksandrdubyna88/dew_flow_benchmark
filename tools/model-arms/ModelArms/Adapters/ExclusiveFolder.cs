using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>One new folder, made EXCLUSIVELY: libc's <c>mkdir</c> where the road has it (every POSIX host) — an entry already at the name, a link included, is <see cref="FolderMade.AlreadyThere"/> and is never adopted — and, on Windows, which has neither the
/// call nor modes, the runtime's <c>CreateDirectory</c> after the caller's own existence check. The one place the module makes a folder it must not share: <c>init</c>'s root and <c>prep-base</c>'s <c>prep-base.prev-&lt;stamp&gt;</c>.</summary>
internal static class ExclusiveFolder
{
    public static FolderMade Make(string path, PlatformRoad road)
    {
        if (road.LibcMkdir)
        {
            return KernelFacts.MakeFolder(path);
        }

        Directory.CreateDirectory(path);
        return new FolderMade.Made();
    }
}
