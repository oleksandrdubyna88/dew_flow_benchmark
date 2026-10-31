namespace ModelArms.Adapters;

/// <summary>What one attempt to make a folder EXCLUSIVELY came to. Three states: <see cref="Made"/> (this call created it), <see cref="AlreadyThere"/> (something was
/// at the name, and is left exactly as it was: a configuration problem, exit 4) and <see cref="Failed"/> (the host would not, with its own words: exit 3).</summary>
internal abstract record FolderMade
{
    private FolderMade() { }

    public sealed record Made : FolderMade;

    public sealed record AlreadyThere : FolderMade;

    public sealed record Failed(string Reason) : FolderMade;
}
