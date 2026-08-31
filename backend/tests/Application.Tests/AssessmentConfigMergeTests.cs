using System.Text.Json;
using System.Text.Json.Nodes;
using Academy.Application.Assessments;

namespace Academy.Application.Tests;

/// <summary>
/// The config is one jsonb bag edited by several admin screens that each model a different subset
/// of it. Under whole-document replace every screen silently destroyed the fields it did not model.
/// Presence — not nullness — decides: omit a key to keep it, send it to change it.
/// </summary>
public class AssessmentConfigMergeTests
{
    private const string Stored = """
    {
      "passThreshold": 9,
      "retakeCap": 2,
      "proctoringEnabled": true,
      "audioPlayLimit": 3,
      "timeLimitMinutes": 20,
      "sections": [
        { "section": "Listening", "questions": 50, "minutes": 35, "audioRef": "audio/keep-me.mp3" },
        { "section": "Reading",   "questions": 50, "minutes": 55, "audioRef": null }
      ]
    }
    """;

    private static JsonObject In(string json) => JsonNode.Parse(json)!.AsObject();
    private static JsonObject Out(string merged) => JsonNode.Parse(merged)!.AsObject();

    [Fact]
    public void A_key_the_client_did_not_send_is_preserved()
    {
        // This is the live bug: the test editor sends passThreshold/retakeCap/proctoringEnabled
        // and hard-codes the rest, resetting audioPlayLimit on every save.
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""{ "passThreshold": 12 }""")));

        Assert.Equal(12, (int)merged["passThreshold"]!);
        Assert.Equal(3, (int)merged["audioPlayLimit"]!);      // untouched
        Assert.Equal(20, (int)merged["timeLimitMinutes"]!);   // untouched
        Assert.Equal(2, (int)merged["retakeCap"]!);           // untouched
    }

    [Fact]
    public void An_explicit_null_still_clears_a_field()
    {
        // "Clear this" must stay expressible, or the remove buttons in the admin UI break.
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""{ "retakeCap": null }""")));

        Assert.Null(merged["retakeCap"]);
        Assert.Equal(9, (int)merged["passThreshold"]!);
    }

    [Fact]
    public void A_section_rewritten_without_audioRef_keeps_its_recording()
    {
        // The composer bug, generalised: a screen that edits the layout must not destroy a
        // recording attached to a section it was not editing.
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""
            { "sections": [ { "section": "Listening", "questions": 40, "minutes": 30 } ] }
            """)));

        var listening = merged["sections"]!.AsArray()[0]!.AsObject();
        Assert.Equal(40, (int)listening["questions"]!);
        Assert.Equal("audio/keep-me.mp3", (string)listening["audioRef"]!);
    }

    [Fact]
    public void A_section_can_still_clear_its_own_recording()
    {
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""
            { "sections": [ { "section": "Listening", "audioRef": null } ] }
            """)));

        Assert.Null(merged["sections"]!.AsArray()[0]!.AsObject()["audioRef"]);
    }

    [Fact]
    public void Sections_omitted_from_the_request_are_removed()
    {
        // The array IS the layout, so dropping a section is how one is deleted. Only the FIELDS
        // of a surviving section merge — otherwise a section could never be removed.
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""
            { "sections": [ { "section": "Reading", "questions": 50, "minutes": 55 } ] }
            """)));

        var sections = merged["sections"]!.AsArray();
        Assert.Single(sections);
        Assert.Equal("Reading", (string)sections[0]!.AsObject()["section"]!);
    }

    [Fact]
    public void An_empty_sections_array_clears_the_layout()
    {
        // A gating test has no sections; sending [] must actually empty it.
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""{ "sections": [] }""")));
        Assert.Empty(merged["sections"]!.AsArray());
    }

    [Fact]
    public void A_new_section_is_added_as_sent()
    {
        var merged = Out(AssessmentConfigMerge.Merge(Stored, In("""
            { "sections": [ { "section": "Structure", "questions": 40, "minutes": 25 } ] }
            """)));

        var s = merged["sections"]!.AsArray()[0]!.AsObject();
        Assert.Equal("Structure", (string)s["section"]!);
        Assert.Equal(40, (int)s["questions"]!);
    }

    [Fact]
    public void Merging_onto_nothing_yields_the_request()
    {
        // The create path: there is no stored document to preserve.
        var merged = Out(AssessmentConfigMerge.Merge(null, In("""{ "passThreshold": 5 }""")));
        Assert.Equal(5, (int)merged["passThreshold"]!);
    }

    [Fact]
    public void A_corrupt_stored_document_does_not_throw()
    {
        var merged = Out(AssessmentConfigMerge.Merge("not json at all", In("""{ "passThreshold": 5 }""")));
        Assert.Equal(5, (int)merged["passThreshold"]!);
    }

    [Fact]
    public void The_merged_document_deserializes_for_validation()
    {
        // Validation must run against the END STATE, not the partial request that produced it.
        var merged = AssessmentConfigMerge.Merge(Stored, In("""{ "passThreshold": 11 }"""));
        var config = AssessmentConfigMerge.ToConfig(merged, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(11, config.PassThreshold);
        Assert.Equal(3, config.AudioPlayLimit);
        Assert.Equal(2, config.Sections.Count);
        Assert.Equal("audio/keep-me.mp3", config.Sections[0].AudioRef);
    }
}
