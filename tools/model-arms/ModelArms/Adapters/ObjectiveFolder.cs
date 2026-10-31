using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>Which folder <c>out/objective</c> was when <c>checks</c> made it: on the kernel road its device and inode (a name that now reaches another folder — or a link to one — is told by them); on any other road that it was a folder and not a link, which is all the runtime can say.
/// Two <see cref="FolderIdentity"/> are equal when the same folder was reached.</summary>
internal sealed record FolderIdentity(bool Kernel, ulong Device, ulong Inode);

/// <summary>The identity of the objective folder (E3.S5 step 7): taken right after the harness made the folder, taken again after every child has ended, compared. The arm's code runs between the two with the operator's rights, so it can rename the folder aside and put a link — or another folder — at the name; a harness that then
/// wrote <c>stages.json</c> or read the CTRF through that name would write into, and read from, a place the arm chose. The facts come from the same readers the trust check uses (<see cref="RootFacts"/>: <c>statx</c> on the kernel road, the runtime's attributes on the portable one); Windows, which has no trust road, is asked its attributes
/// directly. <b>Residual:</b> on the portable road a swap that puts ANOTHER real folder at the name is not told (no inode) — the road's printed line says the inode is not verified; a link is.</summary>
internal static class ObjectiveFolder
{
    /// <summary>The identity of the folder at <paramref name="path"/> on <paramref name="road"/>, or why it is none (not there, not a folder, a link).</summary>
    public static Outcome<FolderIdentity> Take(string path, PlatformRoad road) =>
        road.Trust == TrustRoad.NotPerformed ? ByAttributes(path) : ByFacts(path, road);

    /// <summary>The empty text when <paramref name="path"/> still reaches the folder <paramref name="made"/> names; otherwise the sentence saying what it reaches now.</summary>
    public static string Changed(string path, FolderIdentity made, PlatformRoad road) =>
        Take(path, road).Match(now => now == made ? string.Empty : "it is another folder", reason => reason);

    private static Outcome<FolderIdentity> ByFacts(string path, PlatformRoad road) =>
        RootFacts.ReadFacts(path, FileKind.Directory, road) switch
        {
            FactsRead.Read { Facts: { Source: FactsSource.Kernel } facts } => Outcome<FolderIdentity>.Success(new FolderIdentity(true, facts.Identity.Device, facts.Identity.Inode)),
            FactsRead.Read => Outcome<FolderIdentity>.Success(new FolderIdentity(false, 0, 0)),
            FactsRead.Refused refused => Outcome<FolderIdentity>.Failure(refused.Reason),
            FactsRead.Unread unread => Outcome<FolderIdentity>.Failure(unread.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Outcome<FolderIdentity> ByAttributes(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint) ? Outcome<FolderIdentity>.Failure($"{path} is a link")
                : !attributes.HasFlag(FileAttributes.Directory) ? Outcome<FolderIdentity>.Failure($"{path} is not a folder")
                : Outcome<FolderIdentity>.Success(new FolderIdentity(false, 0, 0));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<FolderIdentity>.Failure($"{path}: {ex.Message}");
        }
    }
}
