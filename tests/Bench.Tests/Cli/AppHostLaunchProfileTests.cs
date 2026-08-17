using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The orchestrator's launch profile has to be able to START.
/// <para>
/// Observed 2026-08-17, the first time anybody ran it: the only profile declared an <c>http</c>
/// <c>applicationUrl</c>, and Aspire refuses that unless <c>ASPIRE_ALLOW_UNSECURED_TRANSPORT</c> is set —
/// so <c>dotnet run --project hosts/AppHost</c> died in startup validation, before any container existed.
/// The AppHost is the only thing that provides this benchmark's persistent database, so an unstartable
/// profile means the results have nowhere to live: the run I was verifying went into a hand-rolled
/// throwaway Postgres instead, which is precisely the "a measurement taken in March is comparable in
/// August" guarantee that file's own comment makes.
/// </para>
/// <para>
/// A config test rather than a launch: starting an orchestrator in a unit suite would take Docker, a
/// dashboard and a minute. What broke here was a JSON file, and reading the JSON file is what catches it.
/// </para></summary>
public sealed class AppHostLaunchProfileTests
{
    private static readonly string Path = System.IO.Path.Combine(
        Repository.Root, "hosts", "AppHost", "Properties", "launchSettings.json");

    [Fact]
    public void Every_profile_is_one_Aspire_will_actually_start()
    {
        foreach (var profile in Profiles())
        {
            var url = Text(profile.Value, "applicationUrl");
            var unsecuredAllowed = Text(Environment(profile.Value), "ASPIRE_ALLOW_UNSECURED_TRANSPORT");

            // Either the dashboard is served over https — the arrangement the sibling repository's AppHost
            // uses — or the override is set deliberately. Neither, and startup validation ends the process.
            (url.Contains("https://", StringComparison.Ordinal)
                || unsecuredAllowed.Equals("true", StringComparison.OrdinalIgnoreCase))
                .Should().BeTrue(
                    $"profile '{profile.Name}' declares applicationUrl '{url}', and Aspire refuses a non-https "
                    + "dashboard unless ASPIRE_ALLOW_UNSECURED_TRANSPORT is true — it would die in startup "
                    + "validation with no container and no database for results to live in");
        }
    }

    [Fact]
    public void The_postgres_password_is_supplied_so_the_parameter_resolves()
    {
        foreach (var profile in Profiles())
        {
            // An unresolved parameter does not fail: the orchestrator waits, with a running dashboard and no
            // containers, which reads as a hang rather than a missing value. The AppHost's own comment records
            // that this was observed here.
            Text(Environment(profile.Value), "Parameters__bench-postgres-password")
                .Should().NotBeEmpty($"profile '{profile.Name}' would leave the orchestrator waiting on a parameter");
        }
    }

    [Fact]
    public void The_environment_is_Development_so_user_secrets_load_at_all()
    {
        foreach (var profile in Profiles())
        {
            Text(Environment(profile.Value), "DOTNET_ENVIRONMENT")
                .Should().Be("Development", "in Production the parameter store is not even consulted");
        }
    }

    private static IEnumerable<JsonProperty> Profiles()
    {
        var json = JsonDocument.Parse(File.ReadAllText(Path));
        var profiles = json.RootElement.GetProperty("profiles");

        profiles.EnumerateObject().Should().NotBeEmpty("an AppHost with no launch profile cannot be run at all");

        return profiles.EnumerateObject().ToList();
    }

    private static JsonElement Environment(JsonElement profile) =>
        profile.TryGetProperty("environmentVariables", out var variables) ? variables : default;

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

/// <summary>Where the repository root is, from a test binary buried in <c>bin/Release/net10.0</c>.
/// <para>
/// Walks up looking for the solution file rather than counting <c>..</c> segments: the count differs
/// between Debug and Release and between a local run and CI, and a path that is right in one of them is a
/// test that is red in the other for no reason anybody can see.
/// </para></summary>
internal static class Repository
{
    public static string Root { get; } = Find(AppContext.BaseDirectory);

    private static string Find(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"no *.slnx above '{start}' — this test reads repository files and cannot guess where they are");
    }
}
