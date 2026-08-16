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
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "add" => await AddAsync(command, registry, clock, output, error, cancellationToken),
            "list" => await ListAsync(command, registry, secrets, output, error, cancellationToken),
            "disable" or "enable" => await EnabledAsync(command, registry, output, error, cancellationToken),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench models needs an action — 'add', 'list', 'disable' or 'enable'"
                    : $"unknown models action '{other}' — try 'add', 'list', 'disable' or 'enable'"),
        };

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
