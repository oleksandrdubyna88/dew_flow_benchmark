using System.Globalization;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>The verbs' ONE aside helper (question 7's renames, the one site the scan of <c>IsolationRecoveryTests</c> allows): <c>.locked-modes</c> → <c>.locked-modes.restored-&lt;utc&gt;</c> and <c>.locked-modes.applied</c> → <c>.locked-modes.applied.restored-&lt;utc&gt;</c>, <c>&lt;utc&gt;</c> being <c>yyyyMMdd'T'HHmmss'Z'</c>. A name already taken (two cycles in one
/// second) gets <c>-2</c> … <c>-9</c> through <see cref="File.Move(string, string, bool)"/> without overwrite, so no record is ever lost; a link at the source is renamed as the link it is. The answer is the name the file now has, or why it could not be moved.</summary>
internal static class SnapshotAside
{
    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const int MostCycles = 9;

    public static Outcome<string> Snapshot(RootPaths root, TimeProvider clock) => Move(root.LockedModes, stamp => root.LockedModesRestored(stamp), clock);

    public static Outcome<string> Applied(RootPaths root, TimeProvider clock) => Move(root.LockedModesApplied, stamp => root.LockedModesAppliedRestored(stamp), clock);

    private static Outcome<string> Move(string source, Func<string, string> named, TimeProvider clock)
    {
        var stamp = clock.GetUtcNow().ToString(StampFormat, CultureInfo.InvariantCulture);
        for (var attempt = 1; attempt <= MostCycles; attempt++)
        {
            var target = named(attempt == 1 ? stamp : $"{stamp}-{attempt}");
            try
            {
                File.Move(source, target, overwrite: false);
                return Outcome<string>.Success(target);
            }
            catch (IOException) when (Path.Exists(target) && attempt < MostCycles)
            {
                // taken by an earlier cycle of this second: the next name
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Outcome<string>.Failure($"{source} could not be moved aside to {target}: {ex.Message}");
            }
        }

        return Outcome<string>.Failure($"{source} could not be moved aside: {MostCycles} records of this second already stand");
    }
}
