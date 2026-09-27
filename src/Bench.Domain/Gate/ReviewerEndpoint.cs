using System.Net;
using Bench.Domain.Registry;

namespace Bench.Domain.Gate;

/// <summary>How the product reaches a reviewer — the product's own runtime WORDS, one member per word.
/// <para>
/// There is no <c>cli</c> member, on purpose. The product has no runtime called <c>cli</c>: it knows which
/// command-line agent to launch by the runtime word itself (<c>codex</c>, <c>gemini</c>, <c>claude</c>,
/// <c>antigravity</c>), and a word it does not know is run on Codex. A row that said <c>cli</c> therefore ran a
/// Claude or Gemini reviewer on the Codex CLI without a word of complaint — the measurement of one model
/// filed under another's name. The set is pinned against the product's own list by a test
/// (<c>coai · src_mcp/runners/Reviewers/ReviewerRuntime.cs</c>, <c>RuntimeNames</c>).
/// </para></summary>
public enum ReviewerRuntime
{
    /// <summary>An OpenAI-compatible HTTP endpoint the product calls itself, in a dialect.</summary>
    Api,

    /// <summary>The Codex CLI, launched by the product.</summary>
    Codex,

    /// <summary>The Gemini CLI, launched by the product.</summary>
    Gemini,

    /// <summary>The Claude Code CLI, launched by the product.</summary>
    Claude,

    /// <summary>The Antigravity CLI, launched by the product.</summary>
    Antigravity,

    /// <summary>A model served on this machine.</summary>
    Local,

    /// <summary>A Team server answers; the row names the vendor that server knows.</summary>
    Remote,
}

public static class ReviewerRuntimes
{
    /// <summary>A vendor's own command-line agent, launched by the product — no HTTP for the tap to sit in
    /// front of, and no metered cost.</summary>
    public static bool IsCli(this ReviewerRuntime runtime) =>
        runtime is ReviewerRuntime.Codex or ReviewerRuntime.Gemini or ReviewerRuntime.Claude or ReviewerRuntime.Antigravity;

    /// <summary>The word the product's vendor row carries — the member's name, lower-cased.</summary>
    public static string Word(this ReviewerRuntime runtime) => runtime.ToString().ToLowerInvariant();
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

    /// <summary>An IPv4-mapped IPv6 address (<c>::ffff:127.0.0.1</c>) IS the IPv4 address it carries, so it is
    /// normalised before any range check: read as IPv6 it is neither loopback nor link-local nor unique-local,
    /// and its sixteen bytes never meet the four-byte private ranges — it passed as a public vendor url.</summary>
    private static bool IsPrivate(IPAddress parsed) =>
        IsPrivateAddress(parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed);

    private static bool IsPrivateAddress(IPAddress address) =>
        IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal || IsPrivateV4(address);

    private static bool IsPrivateV4(IPAddress address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && IsPrivateV4(address.GetAddressBytes());

    private static bool IsPrivateV4(byte[] b) =>
        b[0] == 10
        || b[0] == 172 && b[1] >= 16 && b[1] <= 31
        || b[0] == 192 && b[1] == 168
        || b[0] == 169 && b[1] == 254;
}
