using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModelArms.Core.Tasks;

/// <summary>One line of <c>out/pick-&lt;id&gt;.json</c> (JSON Lines, D22, E4.S2 spec item 8): an INTENT written BEFORE a <c>cherry-pick</c> and its COMPLETION written after it. A crash between the two leaves a record that says a pick was started from a known HEAD, so re-entry
/// tells "it happened" from "it did not" without a patch-id — which an empty commit does not have.</summary>
public abstract record PickEntry
{
    private PickEntry()
    {
    }

    /// <summary><c>{"source":"&lt;sha&gt;","headBefore":"&lt;sha&gt;"}</c>: a pick of <paramref name="Source"/> was about to start with the clone at <paramref name="HeadBefore"/>.</summary>
    public sealed record Intent(string Source, string HeadBefore) : PickEntry;

    /// <summary><c>{"source":"&lt;sha&gt;","picked":"&lt;sha&gt;","patchId":"&lt;hex or empty&gt;"}</c>: <paramref name="Source"/> was carried as <paramref name="PickedSha"/>; the patch-id is empty for a commit without a patch (an empty or a merge commit).</summary>
    public sealed record Picked(string Source, string PickedSha, string PatchId) : PickEntry;
}

/// <summary>What reading the record came to.</summary>
public abstract record PickRecordRead
{
    private PickRecordRead()
    {
    }

    /// <summary>Every line was read; <paramref name="TornTail"/> says the text's last segment was not one whole line (no final <c>\n</c>, or not one record) and is NOT among <paramref name="Valid"/>.</summary>
    public sealed record Lines(IReadOnlyList<PickEntry> Valid, bool TornTail) : PickRecordRead;

    /// <summary>A line BEFORE the last is not one record, or an intent before the last entry was never settled — appending cannot repair either (D22).</summary>
    public sealed record Unreadable(int LineNumber, string Why) : PickRecordRead;
}

/// <summary>The pick record's two kinds of line, read strictly (the member set tells the kinds apart; strings only; shas in hex) and written in one spelling.</summary>
public static partial class PickRecord
{
    private static readonly string[] IntentMembers = ["source", "headBefore"];

    private static readonly string[] PickedMembers = ["source", "picked", "patchId"];

    public static PickRecordRead Parse(string text)
    {
        if (text.Length == 0)
        {
            return new PickRecordRead.Lines([], false);
        }

        var segments = text.Split('\n');
        var terminated = text.EndsWith('\n');
        var count = terminated ? segments.Length - 1 : segments.Length;
        var valid = new List<PickEntry>();
        var tornTail = !terminated;
        for (var index = 0; index < count; index++)
        {
            var last = index == count - 1;
            var read = ReadLine(segments[index]);
            if (read is null && last)
            {
                tornTail = true;
            }
            else if (read is null)
            {
                return new PickRecordRead.Unreadable(index + 1, WhyNot(segments[index]));
            }
            else if (!(last && !terminated))
            {
                valid.Add(read);
            }
        }

        return Unsettled(valid) is { } line ? new PickRecordRead.Unreadable(line, "an intent to pick that was never completed — the next line is not its completion or its retry") : new PickRecordRead.Lines(valid, tornTail);
    }

    /// <summary>The LAST entry when it is an intent (nothing after it can have completed it); otherwise <c>null</c>.</summary>
    public static PickEntry.Intent? Open(IReadOnlyList<PickEntry> valid) => valid.Count > 0 ? valid[^1] as PickEntry.Intent : null;

    public static string Line(PickEntry entry)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            switch (entry)
            {
                case PickEntry.Intent intent:
                    writer.WriteString("source", intent.Source);
                    writer.WriteString("headBefore", intent.HeadBefore);
                    break;
                case PickEntry.Picked picked:
                    writer.WriteString("source", picked.Source);
                    writer.WriteString("picked", picked.PickedSha);
                    writer.WriteString("patchId", picked.PatchId);
                    break;
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Every entry and a newline: the repaired record and the marker's text.</summary>
    public static string Write(IReadOnlyList<PickEntry> entries) => string.Concat(entries.Select(entry => Line(entry) + "\n"));

    /// <summary>The 1-based line number of the first intent that is neither the last entry nor settled by the VERY NEXT line: that line is for the same source and is its completion, or its retry from the same <c>headBefore</c> (a pick that did not happen). In a record this pipeline wrote the next line for a source IS the next line — the pipeline settles an open
    /// intent before it picks anything else — so any other shape is a hand-made or mended record (E4 §8 item 31).</summary>
    private static int? Unsettled(IReadOnlyList<PickEntry> valid)
    {
        for (var index = 0; index < valid.Count - 1; index++)
        {
            if (valid[index] is PickEntry.Intent intent && !Settles(intent, valid[index + 1]))
            {
                return index + 1;
            }
        }

        return null;
    }

    private static bool Settles(PickEntry.Intent intent, PickEntry next) => next switch
    {
        PickEntry.Picked picked => picked.Source == intent.Source,
        PickEntry.Intent retry => retry.Source == intent.Source && retry.HeadBefore == intent.HeadBefore,
        _ => false,
    };

    private static string WhyNot(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object ? "not one of the two record shapes (a member set, a non-text member or a sha that is not hex)" : "not a JSON object";
        }
        catch (JsonException exception)
        {
            return "not JSON: " + exception.Message;
        }
    }

    private static PickEntry? ReadLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var members = document.RootElement.EnumerateObject().ToList();
            if (members.Any(member => member.Value.ValueKind != JsonValueKind.String))
            {
                return null;
            }

            var names = members.Select(member => member.Name).Order(StringComparer.Ordinal).ToList();
            string Text(string name) => members.First(member => member.Name == name).Value.GetString() ?? string.Empty;
            if (names.SequenceEqual(IntentMembers.Order(StringComparer.Ordinal)))
            {
                return Sha().IsMatch(Text("source")) && Sha().IsMatch(Text("headBefore")) ? new PickEntry.Intent(Text("source"), Text("headBefore")) : null;
            }

            if (names.SequenceEqual(PickedMembers.Order(StringComparer.Ordinal)))
            {
                var patchId = Text("patchId");
                return Sha().IsMatch(Text("source")) && Sha().IsMatch(Text("picked")) && (patchId.Length == 0 || Sha().IsMatch(patchId)) ? new PickEntry.Picked(Text("source"), Text("picked"), patchId) : null;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^[0-9a-f]{40}([0-9a-f]{24})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha();
}
