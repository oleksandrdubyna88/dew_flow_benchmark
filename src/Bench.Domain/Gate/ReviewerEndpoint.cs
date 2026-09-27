using System.Net;
using Bench.Domain.Registry;

namespace Bench.Domain.Gate;

/// <summary>How the product reaches a reviewer — the product's own runtime words.</summary>
public enum ReviewerRuntime
{
    /// <summary>An OpenAI-compatible HTTP endpoint the product calls itself, in a dialect.</summary>
    Api,

    /// <summary>A vendor's own command-line agent, launched by the product.</summary>
    Cli,

    /// <summary>A model served on this machine.</summary>
    Local,

    /// <summary>A Team server answers; the row names the vendor that server knows.</summary>
    Remote,
}

/// <summary>Where an <c>api</c> reviewer is — as a VALUE when it is a public vendor url, and as a REFERENCE
/// (the NAME of the environment variable holding it) when it is a machine-local address.
/// <para>
/// <see cref="ModelConfig"/>'s rule, inverted for addresses: a registry row never stores a url because the
/// database is published unedited, but a vendor's public endpoint is not a machine's identity — it is the
/// same string on every machine and a report has to be able to say which pool answered. A loopback or
/// private address IS a machine's identity, so that one is refused as a value and stored as a name.
/// </para></summary>
public abstract record ReviewerEndpoint
{
    private ReviewerEndpoint()
    {
    }

    /// <summary>No endpoint — a <c>cli</c> or <c>remote</c> row, which the product reaches its own way.</summary>
    public sealed record None : ReviewerEndpoint;

    public sealed record Value(string Url) : ReviewerEndpoint;

    public sealed record Reference(string Name) : ReviewerEndpoint;

    public string Canonical => this switch
    {
        Value v => $"url:{v.Url}",
        Reference r => $"ref:{r.Name}",
        _ => "none",
    };

    public static Outcome<ReviewerEndpoint> Parse(string? endpoint)
    {
        var trimmed = (endpoint ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Outcome<ReviewerEndpoint>.Success(new None());
        }

        return ModelConfig.LooksLikeAValue(trimmed) ? AsValue(trimmed) : AsReference(trimmed);
    }

    private static Outcome<ReviewerEndpoint> AsValue(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return Outcome<ReviewerEndpoint>.Failure($"'{url}' is not an absolute url with a host");
        }

        return IsMachineLocal(uri.Host)
            ? Outcome<ReviewerEndpoint>.Failure(
                $"endpoint '{url}' is a machine-local address — store the NAME of the environment variable that holds it, "
                + "so the row can be published without this machine leaving with it; a public vendor url may be a value")
            : Outcome<ReviewerEndpoint>.Success(new Value(url));
    }

    private static Outcome<ReviewerEndpoint> AsReference(string name) =>
        ModelConfig.IsReference(name)
            ? Outcome<ReviewerEndpoint>.Success(new Reference(name))
            : Outcome<ReviewerEndpoint>.Failure(
                $"'{name}' is neither a url nor a usable reference — a public vendor url as a value, or the NAME of the "
                + "environment variable holding a machine-local address (letters, digits, '_', ':' and '.')");

    /// <summary>Whether a host names THIS machine or its network rather than a vendor: loopback, a private
    /// range, link-local, <c>localhost</c>, <c>.local</c>, or a bare name with no dot in it.</summary>
    public static bool IsMachineLocal(string host)
    {
        var lower = host.Trim().TrimStart('[').TrimEnd(']').ToLowerInvariant();

        return lower is "localhost"
            || lower.EndsWith(".local", StringComparison.Ordinal)
            || !lower.Contains('.') && !lower.Contains(':')
            || IPAddress.TryParse(lower, out var address) && IsPrivate(address);
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && IsPrivateV4(bytes);
    }

    private static bool IsPrivateV4(byte[] b) =>
        b[0] == 10
        || b[0] == 172 && b[1] >= 16 && b[1] <= 31
        || b[0] == 192 && b[1] == 168
        || b[0] == 169 && b[1] == 254;
}
