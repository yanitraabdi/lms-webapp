using System.Text.Json;
using System.Text.Json.Nodes;

namespace Academy.Application.Assessments;

/// <summary>
/// Merges an incoming assessment config over the stored one by KEY PRESENCE: a key the client did
/// not send is preserved, a key it did send wins — including when the value is explicitly null.
///
/// This exists because the config is one jsonb bag edited by several admin screens, each modelling
/// a different subset of it. Under whole-document replace, every screen silently destroyed the
/// fields it did not model: the composer wiped each section's audioRef, and the test editor still
/// resets audioPlayLimit, passThreshold and timeLimitMinutes to hard-coded values on every save.
/// Both were found by review rather than by anyone noticing, which is the problem — the loss is
/// invisible until a learner hits it.
///
/// Presence, not nullness, is the signal. That keeps "clear this field" expressible (send the key
/// with null) while making "I don't model this field" safe (omit it).
/// </summary>
public static class AssessmentConfigMerge
{
    private const string SectionsKey = "sections";
    private const string SectionNameKey = "section";

    /// <summary>Merges <paramref name="incoming"/> over <paramref name="storedJson"/>.</summary>
    public static string Merge(string? storedJson, JsonObject? incoming)
    {
        var stored = Parse(storedJson);
        if (incoming is null) return stored.ToJsonString();

        foreach (var (key, value) in incoming)
        {
            // Sections are identified by their section name, not their position, so they merge
            // element-wise: a screen that rewrites the layout must not drop a recording attached
            // to a section it left alone.
            if (string.Equals(key, SectionsKey, StringComparison.OrdinalIgnoreCase)
                && value is JsonArray incomingSections
                && stored[key] is JsonArray storedSections)
            {
                stored[key] = MergeSections(storedSections, incomingSections);
                continue;
            }

            stored[key] = value?.DeepClone();
        }

        return stored.ToJsonString();
    }

    /// <summary>Deserializes the merged document, so validation runs against the END STATE rather
    /// than the partial request that produced it.</summary>
    public static AssessmentConfig ToConfig(string mergedJson, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<AssessmentConfig>(mergedJson, options) ?? new AssessmentConfig();

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json)?.AsObject() ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    private static JsonArray MergeSections(JsonArray stored, JsonArray incoming)
    {
        var result = new JsonArray();
        foreach (var node in incoming)
        {
            if (node is not JsonObject incomingSection)
            {
                result.Add(node?.DeepClone());
                continue;
            }

            var name = NameOf(incomingSection);
            var storedSection = name is null ? null : stored
                .OfType<JsonObject>()
                .FirstOrDefault(s => string.Equals(NameOf(s), name, StringComparison.OrdinalIgnoreCase));

            if (storedSection is null)
            {
                result.Add(incomingSection.DeepClone());
                continue;
            }

            var merged = storedSection.DeepClone().AsObject();
            foreach (var (key, value) in incomingSection) merged[key] = value?.DeepClone();
            result.Add(merged);
        }

        // Sections absent from the request are dropped, not preserved: the array IS the layout, so
        // omitting one is how a section is removed. Only the FIELDS of a surviving section merge.
        return result;
    }

    private static string? NameOf(JsonObject section) =>
        section.TryGetPropertyValue(SectionNameKey, out var v) ? v?.GetValue<string>() : null;
}
