using Bench.Application;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Domain.Runs;

namespace Bench.Cli;

/// <summary>The model registry, from the command line.
/// <para>
/// Every role a model can play — subject, arbiter, and later question author and reviewer — draws from
/// this one list, so a model is added once and named by key everywhere. There is no <c>remove</c>: a run
/// names the key it measured under, so a model is disabled and its history stays readable.
/// </para>
/// <para>
/// <b>What goes in is a reference, not a value.</b> <c>--base-url-ref BENCH_QWEN_URL</c> stores the NAME;
/// the value is read from this machine's environment at use. That is not ceremony: this database is
/// published unedited, and an endpoint or an API key in it is a machine's identity leaving with the
/// results.
/// </para></summary>
public static class ModelsCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        IModelRegistry registry,
        ISecretSource secrets,
        ICliAgentRuntime agents,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "add" => await AddAsync(command, registry, clock, output, error, cancellationToken),
            "list" => await ListAsync(command, registry, secrets, output, error, cancellationToken),
            "probe" => await ProbeAsync(command, registry, secrets, agents, output, error, cancellationToken),
            "disable" or "enable" => await EnabledAsync(command, registry, output, error, cancellationToken),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench models needs an action — 'add', 'list', 'probe', 'disable' or 'enable'"
                    : $"unknown models action '{other}' — try 'add', 'list', 'probe', 'disable' or 'enable'"),
        };

    /// <summary>`bench models probe` — asks a CLI agent one trivial question and reports what came back.
    /// <para>
    /// It exists because of a measured failure mode: <c>CliArgv</c> maps a headless flag per runtime kind, and
    /// only <c>claude -p</c> has ever been run. A wrong flag does not fail — it opens an interactive session that
    /// waits on a terminal nobody is watching, so the symptom is a timeout at the far end of a batch rather than a
    /// message. Two authoring groups cost a 900-second wall each to a related failure.
    /// </para>
    /// <para>
    /// It goes through the SAME path authoring and vetting use — registry reference, <see cref="ISecretSource"/>,
    /// <c>CliArgv</c>, <c>ProcessRunner</c> — so a pass here means those three agree. And it writes NOTHING: a
    /// verification that leaves candidates behind cannot be run twice with a clear conscience.
    /// </para></summary>
    private static async Task<int> ProbeAsync(
        CommandLine command,
        IModelRegistry registry,
        ISecretSource secrets,
        ICliAgentRuntime agents,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var key = command.Value("key");

        if (key.Length == 0)
        {
            return Fail(error, "--key is required — probe asks ONE registry model whether it answers here");
        }

        var found = await registry.FindAsync(key, cancellationToken);

        if (found is not Outcome<RegisteredModel>.Ok(var model))
        {
            return Fail(error, found.Match(_ => string.Empty, reason => reason));
        }

        var executable = ModelResolution.Executable(model, secrets);

        if (executable is not Outcome<string>.Ok(var path))
        {
            return Fail(error, executable.Match(_ => string.Empty, reason => reason));
        }

        output.WriteLine($"probe    {model.Key} · {model.Runtime.ToString().ToLowerInvariant()} · {model.Config.ModelId}");
        output.WriteLine($"exe      {path}");

        var answered = await agents.AskAsync(
            new AgentAsk(
                model.Runtime,
                path,
                "Reply with exactly one word and nothing else: ready",
                Directory.GetCurrentDirectory(),
                TimeSpan.FromSeconds(command.Int("wall-seconds", 90))),
            cancellationToken);

        return answered.Match(
            answer =>
            {
                output.WriteLine($"answer   {answer.Elapsed.TotalSeconds:0.#}s · {answer.ResponseBytes} byte(s)");
                output.WriteLine($"said     {First(answer.Text)}");
                output.WriteLine();
                output.WriteLine($"{model.Key} answers on this machine — its argv, its login and this launcher agree");
                return ExitCodes.Pass;
            },
            reason =>
            {
                error.WriteLine($"bench: {model.Key} did not answer — {reason}");
                error.WriteLine();
                error.WriteLine("A TIMEOUT here usually means the headless flag is wrong for this CLI: the process");
                error.WriteLine("opened an interactive session and waited. A non-zero exit usually means it is not");
                error.WriteLine("logged in. Both are fixed before a batch, never during one.");
                return ExitCodes.Environment;
            });
    }

    private static string First(string text)
    {
        var line = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(string.Empty);
        return line.Length <= 120 ? line : line[..120] + "…";
    }

    private static async Task<int> AddAsync(
        CommandLine command,
        IModelRegistry registry,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ModelRuntimeKind>(command.Value("runtime", "openaiendpoint"), ignoreCase: true, out var runtime))
        {
            return Fail(
                error,
                $"unknown runtime — try {string.Join(", ", Enum.GetNames<ModelRuntimeKind>()).ToLowerInvariant()}");
        }

        var config = ModelConfig.Parse(
            command.Value("model-id"),
            command.Value("base-url-ref"),
            command.Value("api-key-ref"),
            command.Value("executable-ref"),
            Sampling.Deterministic(command.Int("seed", 1)),
            Money(command, "input-cost"),
            Money(command, "output-cost"));

        if (config is not Outcome<ModelConfig>.Ok(var recipe))
        {
            return Fail(error, config.Reason());
        }

        var model = RegisteredModel.Create(
            command.Value("key"), command.Value("display"), runtime, Hosting(command), recipe, clock.GetUtcNow());

        if (model is not Outcome<RegisteredModel>.Ok(var created))
        {
            return Fail(error, model.Reason());
        }

        return (await registry.AddAsync(created, cancellationToken)).Match(
            value =>
            {
                output.WriteLine($"added {value.Stamp}");
                output.WriteLine($"config   {value.Config.Describe}  (references, resolved on this machine at use)");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> ListAsync(
        CommandLine command,
        IModelRegistry registry,
        ISecretSource secrets,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var listed = await registry.ListAsync(command.Has("all"), cancellationToken);

        return listed.Match(
            models =>
            {
                output.WriteLine($"{models.Count} model(s)");

                foreach (var model in models)
                {
                    // Whether its references resolve HERE is printed beside it, because that is the fact
                    // that decides whether a test can use it — and the alternative is finding out three
                    // hours into a sweep.
                    var state = model.Enabled ? "enabled " : "disabled";
                    output.WriteLine($"  {state}  {model.Stamp,-44}  {Reachable(model, secrets)}");
                }

                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static string Reachable(RegisteredModel model, ISecretSource secrets) =>
        model.Config.References.Count == 0
            ? "no references"
            : string.Join(
                ", ",
                model.Config.References.Select(r => secrets.Resolve(r) is Outcome<string>.Ok ? $"{r} ✓" : $"{r} ✗ unset"));

    private static async Task<int> EnabledAsync(
        CommandLine command,
        IModelRegistry registry,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var key = command.Value("key");

        if (key.Length == 0)
        {
            return Fail(error, "--key is required");
        }

        var enable = command.Operand(0) == "enable";

        return (await registry.SetEnabledAsync(key, enable, cancellationToken)).Match(
            model =>
            {
                output.WriteLine($"{model.Key} is now {(model.Enabled ? "enabled" : "disabled")}"
                    + (model.Enabled ? string.Empty : " — tests that already name it keep their results"));
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static ModelHosting Hosting(CommandLine command) =>
        command.Value("hosting", "local").Equals("cloud", StringComparison.OrdinalIgnoreCase)
            ? ModelHosting.Cloud
            : ModelHosting.Local;

    private static decimal Money(CommandLine command, string name) =>
        decimal.TryParse(
            command.Value(name, "0"),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;

    private static int Fail(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return ExitCodes.Configuration;
    }

    private static string Reason<T>(this Outcome<T> outcome) => outcome.Match(_ => string.Empty, reason => reason);
}
