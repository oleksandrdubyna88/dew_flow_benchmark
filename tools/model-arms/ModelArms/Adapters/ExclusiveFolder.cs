using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>One new folder, made EXCLUSIVELY: libc's <c>mkdir</c> where the road has it (every POSIX host) — an entry already at the name, a link included, is <see cref="FolderMade.AlreadyThere"/> and is never adopted — and, on Windows, which has neither the
/// call nor modes, a check then <c>CreateDirectory</c> (best effort, said below). The one place the module makes a folder it must not share: <c>init</c>'s root and <c>prep-base</c>'s <c>prep-base.prev-&lt;stamp&gt;</c>.</summary>
internal static class ExclusiveFolder
{
    public static FolderMade Make(string path, PlatformRoad road)
    {
        if (road.LibcMkdir)
        {
            return KernelFacts.MakeFolder(path);
        }

        return MadeByTheRuntime(path);
    }

    /// <summary>Where there is no libc <c>mkdir</c> (Windows): an entry at the name — a folder, a file, a link — is <see cref="FolderMade.AlreadyThere"/> and left as it was, checked then created. That is BEST EFFORT, not exclusive: a folder made between the check and
    /// <c>CreateDirectory</c> is adopted, because the runtime has no create-that-fails-if-present for a folder.</summary>
    private static FolderMade MadeByTheRuntime(string path)
    {
        if (KernelFacts.Present(path) is not Outcome<bool>.Ok present)
        {
            return new FolderMade.Failed($"'{path}': the host could not say whether it exists");
        }

        if (present.Value)
        {
            return new FolderMade.AlreadyThere();
        }

        try
        {
            Directory.CreateDirectory(path);
            return new FolderMade.Made();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FolderMade.Failed($"'{path}': {ex.Message}");
        }
    }
}
