using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Targets;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Cli;

/// <summary><c>bench gate reviewers add|list|retire</c>, <c>bench gate probe</c>, <c>bench gate suite verify</c> — the
/// catalog, the no-cost wiring check, and the proof a suite's clones are where it says.</summary>
public static class GateToolsCommand
{
    public static async Task<int> ReviewersAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (GateCliInputs.Connection(command).Length == 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "gate reviewers needs --db (or BENCH_DB)");
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}");
        }

        var catalog = new PostgresGateReviewerCatalog(db);

        return command.Operand(1) switch
        {
            "add" => await AddAsync(command, catalog, output, error, cancellationToken),
            "list" => await ListAsync(command, catalog, output, cancellationToken),
            "retire" => await RetireAsync(command, catalog, output, error, cancellationToken),
            _ => GateRunCommand.Refuse(error, ExitCodes.Configuration, "gate reviewers needs add, list or retire"),
        };
    }

    /// <summary><c>bench gate probe --coai-exe … --reviewers id [--creds-key-from-coai-settings]</c> — the calibration
    /// harness's <c>check.py</c>: start the product with a reviewer's environment in a throwaway data directory, list its
    /// tools and ask <c>providers</c> (which reads the vault and says whether the row is runnable). No model is called.</summary>
    public static async Task<int> ProbeAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var exe = command.Value("coai-exe");
        var reviewerId = command.List("reviewers").FirstOrDefault() ?? string.Empty;

        if (exe.Length == 0 || reviewerId.Length == 0 || GateCliInputs.Connection(command).Length == 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "gate probe needs --coai-exe, --reviewers <id> and --db");
        }

        if (!File.Exists(exe))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the product binary {Path.GetFileName(exe)} is not there");
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        var reviewer = await GateReviewerId.Parse(reviewerId).Match(
            id => new PostgresGateReviewerCatalog(db).GetAsync([id], cancellationToken),
            reason => Task.FromResult(Outcome<IReadOnlyList<GateReviewer>>.Failure(reason)));

        return reviewer is Outcome<IReadOnlyList<GateReviewer>>.Ok ok
            ? await ProbeWithAsync(command, exe, ok.Value[0], output, error, cancellationToken)
            : GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<IReadOnlyList<GateReviewer>>.Fail)reviewer).Reason);
    }

    /// <summary><c>bench gate suite verify --suite-file … [--checkout-root …]</c> — every task's clone at its variant head,
    /// its plan committed there. A task that fails is named; the suite's private names are never printed.</summary>
    public static async Task<int> VerifySuiteAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var path = command.Value("suite-file", Environment.GetEnvironmentVariable("BENCH_GATE_SUITE") ?? string.Empty);

        if (path.Length == 0 || !File.Exists(path))
        {
            return GateRunCommand.Refuse(error, path.Length == 0 ? ExitCodes.Configuration : ExitCodes.Environment, "gate suite verify needs --suite-file, and the file must exist");
        }

        var suite = GateSuiteFile.Parse(await File.ReadAllTextAsync(path, cancellationToken));

        if (suite is not Outcome<GateSuite>.Ok { Value: var frozen })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<GateSuite>.Fail)suite).Reason);
        }

        var provider = new GitCheckoutProvider(CheckoutCacheOptions.Under(command.Value("checkout-root", RunCommand.DefaultCheckoutRoot)), NullLogger<GitCheckoutProvider>.Instance);
        var failures = 0;

        foreach (var task in frozen.Tasks)
        {
            var verdict = await VerifyTaskAsync(provider, task, cancellationToken);
            failures += verdict.Length > 0 ? 1 : 0;
            output.WriteLine($"{(verdict.Length > 0 ? "refused" : "ok"),-14} {task.Id} ({task.Language}) at {task.Case.VariantHead.Short}{(verdict.Length > 0 ? " — " + verdict : string.Empty)}");
        }

        output.WriteLine($"suite          {frozen.Stamp}: {frozen.Tasks.Count - failures} of {frozen.Tasks.Count} task(s) ready");
        return failures == 0 ? ExitCodes.Pass : ExitCodes.Environment;
    }

    private static async Task<string> VerifyTaskAsync(GitCheckoutProvider provider, GateTask task, CancellationToken cancellationToken)
    {
        var location = task.Repository.Path;
        var url = Directory.Exists(location) ? new Uri(Path.GetFullPath(location)).AbsoluteUri : location;
        var checkout = await RepoUrl.Parse(url).Match(
            repo => provider.EnsureAsync(MeasurementTarget.At(repo, task.Case.VariantHead), cancellationToken),
            reason => Task.FromResult(Outcome<string>.Failure(reason)));

        return checkout switch
        {
            Outcome<string>.Ok ok => PlanVerdict(task, File.Exists(Path.Combine(ok.Value, task.Case.PlanPath))),
            Outcome<string>.Fail fail => $"its checkout could not be made — {fail.Reason}",
            _ => "unreachable",
        };
    }

    /// <summary>A plan is committed at the variant head, or carried by the suite at a path the head does NOT commit — the
    /// read-only checkout holds exactly the committed tree, so the file's presence there is the commit.</summary>
    private static string PlanVerdict(GateTask task, bool committed) => (committed, task.Case.PlanText.Length > 0) switch
    {
        (true, false) or (false, true) => string.Empty,
        (true, true) => $"the suite carries a plan for {task.Case.PlanPath}, which the variant head commits — it describes another tree",
        (false, false) => $"the plan {task.Case.PlanPath} is not committed at the variant head, and the suite carries no planText for it",
    };

    private static async Task<int> ProbeWithAsync(CommandLine command, string exe, GateReviewer reviewer, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "bench-gate-probe", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            output.WriteLine($"probe dir      {root}");
            return await ProbeInAsync(command, exe, reviewer, root, output, error, cancellationToken);
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>The probe's throwaway data directory goes when the probe does; a product still closing a file only leaves
    /// it to the operating system's temp cleanup.</summary>
    private static void TryDelete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the temp folder's owner; never a reason to fail a probe that answered.
        }
    }

    private static async Task<int> ProbeInAsync(CommandLine command, string exe, GateReviewer reviewer, string root, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var secrets = new GateSecrets(new EnvironmentSecrets(), command.Has("creds-key-from-coai-settings"), GateSecrets.DefaultCoaiSettingsFile);
        var run = GateRun.Planned(Guid.CreateVersion7(), GateKind.Plan, "probe", DataDirMode.Isolated, DateTimeOffset.UtcNow);
        var gate = reviewer.Definition.Gates.Kinds.FirstOrDefault();
        var references = secrets.References(reviewer);
        var vendors = references is Outcome<ResolvedReferences>.Ok resolved
            ? CoaiVendorsSetting.From([reviewer], gate, resolved.Value)
            : Outcome<CoaiVendorsSetting>.Failure(((Outcome<ResolvedReferences>.Fail)references).Reason);
        var key = reviewer.Definition.Runtime == ReviewerRuntime.Api ? secrets.CredsKey(reviewer) : Outcome<SecretValue>.Success(SecretValue.None);

        var refusal = (vendors, key) switch
        {
            (Outcome<CoaiVendorsSetting>.Fail f, _) => f.Reason,
            (_, Outcome<SecretValue>.Fail f) => f.Reason,
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, refusal);
        }

        var environment = CoaiEnvironment.For(
            new CoaiEnvironmentInputs(new ArtifactScope(run with { Gate = gate }, Guid.NewGuid(), 1), reviewer, GateTaskId.Parse("probe").Match(t => t, _ => throw new InvalidOperationException()), 1,
                GateRunSettings.With(new Dictionary<string, string>()).Match(s => s, _ => throw new InvalidOperationException()),
                Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty),
                root),
            ((Outcome<CoaiVendorsSetting>.Ok)vendors).Value);
        var child = environment.WithSecret(((Outcome<SecretValue>.Ok)key).Value);
        Directory.CreateDirectory(environment.DataDir);

        var launch = new McpLaunch(exe, [], root, child.Variables, Path.Combine(root, "stderr.txt"), TimeSpan.FromMinutes(2)) { Scrub = child.Scrub };
        var opened = await new McpStdioSessionFactory().OpenAsync(launch, cancellationToken);

        if (opened is not Outcome<IMcpSession>.Ok { Value: var session })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, ((Outcome<IMcpSession>.Fail)opened).Reason);
        }

        await using (session)
        {
            return await AskAsync(session, reviewer, output, error, cancellationToken);
        }
    }

    private static async Task<int> AskAsync(IMcpSession session, GateReviewer reviewer, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var tools = await session.ListToolsAsync(TimeSpan.FromMinutes(1), cancellationToken);
        var providers = await session.CallToolAsync("providers", [], TimeSpan.FromMinutes(2), cancellationToken);

        if (tools is not Outcome<IReadOnlyList<string>>.Ok toolList || providers is not Outcome<McpToolAnswer>.Ok answer)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, "the product did not answer tools/list and providers");
        }

        output.WriteLine($"server         {session.ServerName} {session.ServerVersion}");
        output.WriteLine($"tools          {string.Join(", ", toolList.Value)}");

        foreach (var row in Rows(answer.Value.Text))
        {
            output.WriteLine($"provider       {row}");
        }

        output.WriteLine($"probed         {reviewer.Stamp} — no model was called");
        return ExitCodes.Pass;
    }

    /// <summary>An allow-list of fields per provider row — the probe prints no address and no key.</summary>
    private static IEnumerable<string> Rows(string text)
    {
        var root = GateReplyParserJson(text);
        var rows = (root?["vendors"] ?? root?["providers"]) as JsonArray ?? [];

        return rows.OfType<JsonObject>().Select(r => string.Join(" ", new[] { "id", "runtime", "model", "status", "available", "reason", "auth", "feature" }
            .Where(r.ContainsKey)
            .Select(k => $"{k}={r[k]?.ToJsonString()}")));
    }

    private static JsonObject? GateReplyParserJson(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static async Task<int> AddAsync(CommandLine command, PostgresGateReviewerCatalog catalog, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var reviewer = ReviewerFrom(command);

        if (reviewer is not Outcome<GateReviewer>.Ok { Value: var row })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<GateReviewer>.Fail)reviewer).Reason);
        }

        var added = await catalog.AddAsync(row, cancellationToken);
        if (added is Outcome<GateReviewer>.Fail fail)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason);
        }

        var same = GateReviewerCatalog.SameConfiguration(await catalog.ListAsync(includeRetired: true, cancellationToken)).Where(s => s.Ids.Contains(row.Id));
        output.WriteLine($"added          {row.Stamp} — {row.Definition.Runtime.Word()} {row.Definition.Model}, {row.Definition.Gates.Canonical}");

        foreach (var shared in same)
        {
            output.WriteLine($"note           {shared.Describe}");
        }

        return ExitCodes.Pass;
    }

    private static async Task<int> ListAsync(CommandLine command, PostgresGateReviewerCatalog catalog, TextWriter output, CancellationToken cancellationToken)
    {
        foreach (var r in await catalog.ListAsync(command.Has("all"), cancellationToken))
        {
            var state = r.IsActive ? "active" : $"retired {r.RetiredAt.ToString("u", CultureInfo.InvariantCulture)}";
            output.WriteLine($"{r.Stamp,-40} {r.Definition.Runtime.Word(),-11} {r.Definition.Model,-28} {r.Definition.Gates.Canonical,-18} {state}");
        }

        return ExitCodes.Pass;
    }

    private static async Task<int> RetireAsync(CommandLine command, PostgresGateReviewerCatalog catalog, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var retired = await GateReviewerId.Parse(command.Value("id")).Match(
            id => catalog.RetireAsync(id, DateTimeOffset.UtcNow, cancellationToken),
            reason => Task.FromResult(Outcome<GateReviewer>.Failure(reason)));

        return retired switch
        {
            Outcome<GateReviewer>.Ok ok => Printed(output, $"retired        {ok.Value.Stamp} — still readable; runs measured under it still resolve"),
            Outcome<GateReviewer>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static int Printed(TextWriter output, string line)
    {
        output.WriteLine(line);
        return ExitCodes.Pass;
    }

    /// <summary>A catalog row from flags. References are NAMES; the endpoint is a public vendor url or the NAME of the
    /// variable holding a machine-local one.</summary>
    private static Outcome<GateReviewer> ReviewerFrom(CommandLine command)
    {
        var runtime = Enum.TryParse<ReviewerRuntime>(command.Value("runtime"), ignoreCase: true, out var parsed)
            ? Outcome<ReviewerRuntime>.Success(parsed)
            : Outcome<ReviewerRuntime>.Failure("--runtime is one of api, codex, gemini, claude, antigravity, local, remote");
        var endpoint = ReviewerEndpoint.Parse(command.Value("endpoint"));
        var transport = Thinking(command).Match(
            thinking => ReviewerTransport.Parse(
                command.Value("dialect", "openai"), command.Value("effort", ReviewerTransport.ModuleDefault), command.Int("max-tokens", 8192),
                command.Int("timeout-minutes", 20), command.Int("follow-ups", 3), command.Int("review-minutes", 20), thinking),
            Outcome<ReviewerTransport>.Failure);
        var prices = command.Has("price-in") || TierFlags.Any(command.Has) ? Prices(command) : Outcome<ReviewerPrices>.Success(ReviewerPrices.Unknown);
        var gates = HostedGates.Of([.. command.List("gates").SelectMany(g => Enum.TryParse<GateKind>(g, true, out var k) ? [k] : Array.Empty<GateKind>())]);

        return (runtime, endpoint, transport, prices, gates) switch
        {
            (Outcome<ReviewerRuntime>.Fail f, _, _, _, _) => Outcome<GateReviewer>.Failure(f.Reason),
            (_, Outcome<ReviewerEndpoint>.Fail f, _, _, _) => Outcome<GateReviewer>.Failure(f.Reason),
            (_, _, Outcome<ReviewerTransport>.Fail f, _, _) => Outcome<GateReviewer>.Failure(f.Reason),
            (_, _, _, Outcome<ReviewerPrices>.Fail f, _) => Outcome<GateReviewer>.Failure(f.Reason),
            (_, _, _, _, Outcome<HostedGates>.Fail f) => Outcome<GateReviewer>.Failure($"--gates: {f.Reason}"),
            (Outcome<ReviewerRuntime>.Ok r, Outcome<ReviewerEndpoint>.Ok e, Outcome<ReviewerTransport>.Ok t, Outcome<ReviewerPrices>.Ok p, Outcome<HostedGates>.Ok g) =>
                ReviewerDefinition.Parse(r.Value, command.Value("model"), e.Value, command.Value("key-name"), command.Value("creds-key-ref"), command.Value("executable-ref"),
                        command.Value("remote-vendor"), t.Value, p.Value, g.Value)
                    .Match(d => GateReviewerId.Parse(command.Value("id")).Match(
                        id => Outcome<GateReviewer>.Success(GateReviewer.Create(id, d, DateTimeOffset.UtcNow)), Outcome<GateReviewer>.Failure), Outcome<GateReviewer>.Failure),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary><c>--thinking on|off</c> in the product's three states (D1): absent is the vendor's default — a row added
    /// without it used to ask for OFF, which the product refuses for xai and glm. The bare flag stays "on", the meaning
    /// rows were added with before.</summary>
    private static Outcome<ThinkingSetting> Thinking(CommandLine command) =>
        (command.Has("thinking"), command.Value("thinking").ToLowerInvariant()) switch
        {
            (false, _) => Outcome<ThinkingSetting>.Success(ThinkingSetting.VendorDefault),
            (_, "true" or "on") => Outcome<ThinkingSetting>.Success(ThinkingSetting.On),
            (_, "off" or "false") => Outcome<ThinkingSetting>.Success(ThinkingSetting.Off),
            (_, var other) => Outcome<ThinkingSetting>.Failure($"--thinking takes on or off (leave it out for the vendor's default), got '{other}'"),
        };

    private static readonly string[] TierFlags = ["price-tier-from", "price-tier-in", "price-tier-cached", "price-tier-out"];

    /// <summary>The base prices and, when given, the long-context tier (D3) — all four tier flags or none, and a tier that
    /// starts at a real token count: <see cref="ReviewerPrices.Of"/> reads a start of 0 as "no tier", so asked for at 0 it
    /// would silently vanish.</summary>
    private static Outcome<ReviewerPrices> Prices(CommandLine command)
    {
        var given = TierFlags.Where(command.Has).ToList();
        var from = given.Count == TierFlags.Length ? command.Int("price-tier-from", 0) : 0;

        return (command.Has("price-in"), given.Count, from) switch
        {
            (false, _, _) => Outcome<ReviewerPrices>.Failure(
                "a price tier needs the base prices it is a tier of — pass --price-in, --price-cached and --price-out with it"),
            (_, 0, _) => ReviewerPrices.Of((decimal)command.Double("price-in", 0), (decimal)command.Double("price-cached", 0), (decimal)command.Double("price-out", 0)),
            (_, < 4, _) => Outcome<ReviewerPrices>.Failure(
                $"a price tier takes all four of {string.Join(", ", TierFlags.Select(f => "--" + f))} — missing {string.Join(", ", TierFlags.Except(given).Select(f => "--" + f))}"),
            (_, _, < 1) => Outcome<ReviewerPrices>.Failure($"--price-tier-from must be at least 1 token, got {from} — a tier from 0 would read as no tier"),
            _ => ReviewerPrices.Of(
                (decimal)command.Double("price-in", 0), (decimal)command.Double("price-cached", 0), (decimal)command.Double("price-out", 0),
                from, (decimal)command.Double("price-tier-in", 0), (decimal)command.Double("price-tier-cached", 0), (decimal)command.Double("price-tier-out", 0)),
        };
    }
}
