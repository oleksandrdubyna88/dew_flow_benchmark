namespace Bench.Application.Bank;

/// <param name="Json">The JSON an agent answered with, taken out of whatever surrounded it.</param>
/// <param name="Said">What it wrote OUTSIDE that JSON. Empty when it answered as asked.</param>
public sealed record AgentPayload(string Json, string Said);

/// <summary>Getting an agent's JSON out of an agent's answer.
/// <para>
/// <b>Extraction, not repair, and the line is worth stating.</b> Taking the payload out of its surroundings
/// changes nothing inside it — the same reason a code fence is unwrapped. Editing fields to make them parse
/// would be the other thing entirely, and it is what makes a batch unattributable.
/// </para>
/// <para>
/// Shared by the authoring pass (which expects an array) and the vetting pass (an object), parameterised by
/// the bracket pair. Written once because it was found the hard way once: first-bracket-to-last-bracket was
/// the obvious rule and it broke inside one live batch, when prose around the answer contained
/// <c>int[] SourceLine</c> and the slice began at a C# array type. A second copy of that lesson would be a
/// second chance to lose it.
/// </para></summary>
public static class AgentJson
{
    /// <summary>The payload, plus anything the agent said before it.
    /// <para>
    /// <paramref name="parses"/> is the acceptance test, and it has to be the real parser rather than a shape
    /// guess: a balanced slice that is not the answer is exactly what the C# array type produced.
    /// </para></summary>
    public static AgentPayload Extract(string text, char open, char close, Func<string, bool> parses)
    {
        var unfenced = Unfence(text);

        if (unfenced.StartsWith(open))
        {
            return new AgentPayload(unfenced, string.Empty);
        }

        for (var opens = unfenced.IndexOf(open); opens >= 0; opens = unfenced.IndexOf(open, opens + 1))
        {
            if (Balanced(unfenced, opens, open, close) is { } candidate && parses(candidate))
            {
                return new AgentPayload(candidate, unfenced[..opens].Trim());
            }
        }

        return new AgentPayload(unfenced, string.Empty);
    }

    /// <summary>Unwraps a code fence and nothing else. Agents wrap JSON in one often enough that refusing it
    /// would reject good work over a formatting habit, while any further repair would start editing the
    /// answer.</summary>
    public static string Unfence(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstBreak = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return firstBreak > 0 && lastFence > firstBreak
            ? trimmed[(firstBreak + 1)..lastFence].Trim()
            : trimmed;
    }

    /// <summary>The bracket-balanced slice starting at <paramref name="opens"/>, or null when it never closes.
    /// String literals are skipped, so a bracket inside a prompt or a note cannot unbalance the count.</summary>
    private static string? Balanced(string text, int opens, char open, char close)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = opens; index < text.Length; index++)
        {
            var character = text[index];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            escaped = inString && character == '\\';
            inString = character == '"' ? !inString : inString;
            depth += !inString && character == open ? 1 : 0;
            depth -= !inString && character == close ? 1 : 0;

            if (depth == 0 && !inString && character == close)
            {
                return text[opens..(index + 1)];
            }
        }

        return null;
    }
}
