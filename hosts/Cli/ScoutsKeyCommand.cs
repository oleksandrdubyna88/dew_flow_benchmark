using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts key init --campaign-key &lt;file&gt;</c> (E2 S4) — the ONE verb that creates the campaign key: 32 random bytes from the
/// operating system, owner-only at creation (<see cref="Bench.Infrastructure.Gate.FileHashKeyFile"/>), created exactly once — a second
/// <c>init</c> at the same path is refused, and no other verb ever regenerates a missing key, because every commitment published so far is
/// verifiable under that key alone. The path (<c>--campaign-key</c>, else <c>BENCH_SCOUT_CAMPAIGN_KEY</c>) is refused when it resolves —
/// junctions followed — inside any git checkout, the trees root, the mirror root or a directory the Claude user settings grant
/// (<see cref="ScoutCampaignKey"/>). The key's bytes are never printed.
/// <para>Exit codes: 0 created · 4 a flag, a refused placement, a key already there.</para></summary>
public static class ScoutsKeyCommand
{
    public static Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        Task.FromResult(command.Operand(1) switch
        {
            "init" => Init(command, output, error),
            var other => GateRunCommand.Refuse(error, ExitCodes.Configuration, other.Length == 0 ? "scouts key needs a sub-verb — init" : $"unknown scouts key sub-verb '{other}' — init"),
        });

    private static int Init(CommandLine command, TextWriter output, TextWriter error)
    {
        var path = ScoutCampaignKey.PathOf(command.Value("campaign-key"));
        var refusal = ScoutCampaignKey.Refusal(path, ScoutsInputs.TreesRootOf(command), ScoutsInputs.CheckoutRootOf(command), ScoutsInputs.PinSourcesOf(command).SettingsFiles);

        if (refusal.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, refusal);
        }

        return ScoutCampaignKey.Create(path) switch
        {
            Outcome<FileHashKey>.Ok => Created(path, output),
            Outcome<FileHashKey>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
            _ => ExitCodes.Configuration,
        };
    }

    private static int Created(string path, TextWriter output)
    {
        output.WriteLine($"key            created at {Path.GetFullPath(path)} — {Bench.Infrastructure.Gate.FileHashKeyFile.KeyBytes} random bytes, owner-only; back it up outside every repository: a lost key makes every commitment unverifiable");
        return ExitCodes.Pass;
    }
}
