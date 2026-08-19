namespace Bench.Domain.Retrieval;

/// <summary>What a retrieval engine actually computed on — host, execution provider and device, together.
/// <para>
/// <b>Together, because on this hardware they cannot be separated.</b> MIGraphX exists only under Linux/WSL —
/// no prebuilt ONNX Runtime ships that provider, and the library is built from source inside the distro —
/// while DirectML is a Windows API with no Linux counterpart. So there is no WSL+DirectML arm and no
/// Windows+MIGraphX arm: *"WSL against Windows"* and *"MIGraphX against DirectML"* are one comparison with
/// two names, and an arm labelled by host alone invites the reading *"Linux is faster"*, which that
/// measurement cannot support and did not test. The canonical form names all three for exactly that reason
/// (<c>todo/PLAN_compute_backend_axis.md</c> §1b).
/// </para>
/// <para>
/// The one arm that DOES separate them is the CPU pair — the provider every host has — which is why
/// <c>windows/cpu/—</c> and <c>wsl/cpu/—</c> are legal values here rather than a special case.
/// </para></summary>
/// <param name="Device">The accelerator, or <c>—</c> when the provider has none. Kept as reported, compared
/// without case: an operator writing <c>r9700</c> against an engine reporting <c>R9700</c> is not a
/// different arm, and case folding cannot merge two device names that differ by more than case.</param>
public sealed record ComputeBackend(string Host, string Provider, string Device)
{
    /// <summary>Reads an arm's name. Structural only — three non-empty segments — and deliberately NOT an
    /// allow-list of hosts and providers this build has heard of.
    /// <para>
    /// An allow-list would make this repository the authority on which compute backends exist in the world,
    /// in a benchmark whose whole premise is *any* engine: an engine reporting <c>macos/coreml/M3</c> has
    /// declared something real, and refusing to represent it would record it as <em>nothing known</em> —
    /// which is a claim about the engine rather than about this build's vocabulary.
    /// </para>
    /// <para>
    /// A typo in an operator's recipe is not lost by that leniency; it is caught downstream and better. The
    /// engine echoes what it actually served on, the two are compared, and a recipe naming
    /// <c>wsl/migrafx/R9700</c> blocks its cell with both values printed — a refusal that names the real
    /// difference, rather than one that only knows the word was not in a list.
    /// </para></summary>
    public static Outcome<ComputeBackend> Parse(string? value)
    {
        var segments = (value ?? string.Empty).Split('/', StringSplitOptions.TrimEntries);

        if (segments.Length != 3 || segments.Any(string.IsNullOrEmpty))
        {
            return Outcome<ComputeBackend>.Failure(
                $"'{value}' is not a compute backend — the form is host/provider/device, all three named, "
                + "because the host and the execution provider cannot be read apart on this hardware "
                + "(a CPU arm writes its device as '—')");
        }

        return Outcome<ComputeBackend>.Success(
            new ComputeBackend(segments[0].ToLowerInvariant(), segments[1].ToLowerInvariant(), segments[2]));
    }

    /// <summary>The arm's name, and the same one the plan and the report use, so a row in a table and a
    /// column in a write-up cannot drift apart.</summary>
    public string Canonical => $"{Host}/{Provider}/{Device}";

    /// <summary>Whether two declarations describe the same arm. Case-insensitive on the device alone —
    /// host and provider are already lowered at the boundary.</summary>
    public bool Same(ComputeBackend other) =>
        Host == other.Host
        && Provider == other.Provider
        && string.Equals(Device, other.Device, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Whether anything is known about the backend — three states, never two.
/// <para>
/// The <see cref="IndexCommit"/> shape, and it exists for the same reason that one does: *matched* and
/// *mismatched* are two answers, and **not declared** is a third that must not read as either. A
/// third-party engine that says nothing about where it computed has not agreed with the recipe — it has
/// said nothing — and an implementation that let silence compare equal would fold an unattributed row into
/// an arm's aggregate, which is the one error indistinguishable from a correct measurement afterwards.
/// </para></summary>
public abstract record BackendDeclaration
{
    private BackendDeclaration() { }

    /// <summary>Nothing is known. The state of every engine that has not been taught to echo, which today
    /// is all of them.</summary>
    public sealed record NotDeclared : BackendDeclaration
    {
        internal NotDeclared() { }

        public override string Canonical => string.Empty;

        public override string Describe => "not declared";
    }

    public sealed record Declared : BackendDeclaration
    {
        internal Declared(ComputeBackend backend) => Backend = backend;

        public ComputeBackend Backend { get; }

        public override string Canonical => Backend.Canonical;

        public override string Describe => Backend.Canonical;
    }

    public static BackendDeclaration None { get; } = new NotDeclared();

    public static BackendDeclaration Of(ComputeBackend backend) => new Declared(backend);

    /// <summary>Reads what an engine reported. An unparseable value is <see cref="None"/> rather than an
    /// error, exactly as <see cref="IndexCommit.Read"/> treats an unreadable stamp: a value this side cannot
    /// read is a thing it does not know, and that state already exists.</summary>
    public static BackendDeclaration Read(string? reported) =>
        ComputeBackend.Parse(reported).Match(Of, _ => None);

    public abstract string Canonical { get; }

    public abstract string Describe { get; }

    /// <summary>Why this echo is not the recipe's arm, or empty when there is nothing to refuse.
    /// <para>
    /// The <c>CorpusIdentity.Refuse</c> shape. Four cases, and the third is the reason the axis exists:
    /// </para>
    /// <list type="bullet">
    /// <item>the recipe names no arm — anything runs, and whatever the engine said is still RECORDED, so a
    /// report can group by an axis nobody planned;</item>
    /// <item>both name the same arm — runs;</item>
    /// <item>they name different arms — refused, with both printed, because the numbers would be real and
    /// the row naming them would describe other hardware;</item>
    /// <item>the recipe names an arm and the engine says nothing — refused unless the operator allows it,
    /// and then the run keeps saying the arm is UNVERIFIED. The <c>--allow-unstamped-index</c> precedent.</item>
    /// </list></summary>
    public string Refuse(BackendDeclaration recipe, bool allowUndeclared) =>
        recipe is Declared wanted ? Against(wanted, allowUndeclared) : string.Empty;

    /// <summary>The three cases that remain once the recipe is known to name an arm.</summary>
    private string Against(Declared wanted, bool allowUndeclared) =>
        this switch
        {
            Declared served when served.Backend.Same(wanted.Backend) => string.Empty,
            Declared served =>
                $"this variant measures '{wanted.Canonical}' and the engine served on '{served.Canonical}' — "
                + "the numbers would be real and the row naming them would describe different hardware",
            _ when allowUndeclared => string.Empty,
            _ =>
                $"this variant measures '{wanted.Canonical}' and the engine declares no backend, so nothing "
                + "can say what it computed on. Pass --allow-undeclared-backend to measure it anyway, and "
                + "the run will keep saying the arm is UNVERIFIED",
        };
}
