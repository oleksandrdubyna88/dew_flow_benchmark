using Bench.Application.Registry;
using Bench.Domain;

namespace Bench.Infrastructure.Models;

/// <summary>Resolves a registry row's references from this machine's environment.
/// <para>
/// The one place a secret or a machine-specific path enters the process, and it is a NAMED one: the
/// registry stores references precisely so that the value lives here, at use, and never in the database
/// this project publishes.
/// </para>
/// <para>
/// A reference that does not resolve is a refusal carrying the reference's NAME — not an empty string,
/// which would travel on to be refused three layers away as "no base url" and send an operator looking at
/// the wrong thing.
/// </para></summary>
public sealed class EnvironmentSecrets : ISecretSource
{
    public Outcome<string> Resolve(string reference)
    {
        var value = (Environment.GetEnvironmentVariable(reference) ?? string.Empty).Trim();

        return value.Length > 0
            ? Outcome<string>.Success(value)
            : Outcome<string>.Failure(
                $"the environment variable '{reference}' is unset on this machine — a registry row names references, "
                + "and this one resolves to nothing");
    }
}
