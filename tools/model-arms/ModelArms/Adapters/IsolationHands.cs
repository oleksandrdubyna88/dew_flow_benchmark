namespace ModelArms.Adapters;

/// <summary>The test's hands on either isolation adapter, handed in by <c>FolderIsolations.For</c>; production passes <c>null</c>. Internal: nothing in configuration reaches it.</summary>
/// <param name="SetMode">(descriptor — <c>-1</c> on the runtime road, path, mode) → 0 or an errno, REPLACING the chmod; the path lets a hand single out one entry by name. A stand-in that returns 0 and changes nothing is a file system that ignores modes (a Windows drive under WSL).</param>
/// <param name="IsRoot">Replaces the check of the effective uid.</param>
/// <param name="AfterEachMode">(index, path) after each mode of the apply — the killed-half-way seam throws, or kills the process.</param>
/// <param name="BeforeApply">Between the snapshot write and the first mode — the late-snapshot witness.</param>
internal sealed record IsolationHands(Func<int, string, int, int>? SetMode, Func<bool>? IsRoot, Action<int, string>? AfterEachMode, Action? BeforeApply);
