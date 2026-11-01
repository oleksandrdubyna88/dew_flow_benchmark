namespace ModelArms.Core.Isolation;

/// <summary>The order <c>unlock</c> restores in: the reverse of the lock's (<c>lock.sh</c>'s <c>tac</c>), so a folder is opened again after the ones that were locked inside it.</summary>
public static class IsolationRestoreOrder
{
    public static IReadOnlyList<SavedMode> Of(IReadOnlyList<SavedMode> lockOrder) => [.. lockOrder.Reverse()];
}
