using Academy.Application.Assessments;
using Academy.Domain.Enums;

namespace Academy.Application.Tests;

/// <summary>
/// The import format is a transcription target for a non-technical author, so every rule here is
/// really a question of what happens when a human gets it wrong. The answer is always: name the
/// row, name the column, say it in Bahasa Indonesia, and reject the WHOLE file — a partial import
/// leaves the author unsure what landed.
/// </summary>
public class QuestionImportParserTests
{
    private const string Headers = "id,section,prompt,choice_a,choice_b,choice_c,choice_d,answer,passage_id,audio_file,tags";

    /// <summary>Builds a workbook from comma-separated cell values, in Headers order.</summary>
    private static ImportWorkbook Sheet(params string[] rows) => Sheet([], rows);

    private static ImportWorkbook Sheet(IEnumerable<PassageRow> passages, params string[] rows)
    {
        var columns = Headers.Split(',');
        var parsed = rows.Select((r, i) =>
        {
            var values = r.Split('|');
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < columns.Length; c++)
                cells[columns[c]] = c < values.Length ? values[c] : "";
            return new QuestionRow(i + 2, cells);   // row 1 is the header
        }).ToList();
        return new ImportWorkbook(columns, parsed, passages.ToList());
    }

    // id|section|prompt|a|b|c|d|answer|passage|audio|tags
    private const string Good = "L01|Listening|What time?|Nine|Ten|Eleven|Twelve|B||L01.mp3|easy";

    [Fact]
    public void A_clean_row_parses()
    {
        var result = QuestionImportParser.Parse(Sheet(Good));

        Assert.Empty(result.Errors);
        var q = Assert.Single(result.Questions);
        Assert.Equal("L01", q.ExternalId);
        Assert.Equal(QuestionSection.Listening, q.Section);
        Assert.Equal("What time?", q.Prompt);
        Assert.Equal(new[] { "Nine", "Ten", "Eleven", "Twelve" }, q.Choices);
        Assert.Equal("audio/l01.mp3", q.AudioRef);
        Assert.Equal(new[] { "easy" }, q.Tags);
    }

    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("C", 2)]
    [InlineData("D", 3)]
    [InlineData("c", 2)]
    public void A_letter_answer_maps_to_its_index(string letter, int expected)
    {
        var result = QuestionImportParser.Parse(Sheet($"R01|Reading|Q|A1|A2|A3|A4|{letter}|||"));

        Assert.Empty(result.Errors);
        Assert.Equal(expected, Assert.Single(result.Questions).CorrectIndex);
    }

    [Theory]
    [InlineData("E")]
    [InlineData("2")]      // Excel coercion, or an author who counted instead of lettering
    [InlineData("A,C")]    // multi-answer: the runtime accepts one int per question
    [InlineData("")]
    [InlineData("AB")]
    public void An_answer_that_is_not_a_single_letter_A_to_D_is_an_error(string answer)
    {
        var result = QuestionImportParser.Parse(Sheet($"R01|Reading|Q|A1|A2|A3|A4|{answer}|||"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.Row);
        Assert.Equal("answer", error.Column);
        Assert.Empty(result.Questions);
    }

    [Fact]
    public void An_answer_pointing_at_a_blank_choice_is_an_error()
    {
        var result = QuestionImportParser.Parse(Sheet("R01|Reading|Q|A1|A2|||D|||"));

        Assert.Equal("answer", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void Fewer_than_two_choices_is_an_error()
    {
        var result = QuestionImportParser.Parse(Sheet("R01|Reading|Q|Only one||||A|||"));

        Assert.Equal("choice_b", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void A_gap_in_the_choices_is_an_error()
    {
        // "A, blank, C" is ambiguous: does answer=C mean the third column or the second choice?
        // Refusing the sheet is the only reading that cannot silently mis-key an exam.
        var result = QuestionImportParser.Parse(Sheet("R01|Reading|Q|A1||A3|A4|C|||"));

        Assert.Equal("choice_b", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void An_unknown_section_is_an_error_that_lists_the_valid_names()
    {
        var result = QuestionImportParser.Parse(Sheet("R01|Speaking|Q|A1|A2|||A|||"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("section", error.Column);
        Assert.Contains("Listening", error.Message);
        Assert.Contains("Reading", error.Message);
    }

    [Fact]
    public void A_numeric_section_is_an_error_not_an_enum_ordinal()
    {
        // Enum.TryParse happily reads "0" as Listening. An author who typed a number meant nothing
        // of the sort, and Excel turns plenty of things into numbers on its own.
        var result = QuestionImportParser.Parse(Sheet("R01|0|Q|A1|A2|||A|||"));

        Assert.Equal("section", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void A_blank_prompt_is_an_error()
    {
        var result = QuestionImportParser.Parse(Sheet("R01|Reading||A1|A2|||A|||"));

        Assert.Equal("prompt", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void Line_breaks_inside_a_prompt_survive()
    {
        // A listening conversation is three lines: two speakers and the question. This is the
        // reason the format is a spreadsheet and not the clipboard-TSV the score-band screen uses.
        var conversation = "M: Are you going?\nW: I can't.\nWhat does the woman mean?";
        var result = QuestionImportParser.Parse(Sheet($"L01|Listening|{conversation}|A1|A2|||A|||"));

        Assert.Empty(result.Errors);
        Assert.Equal(conversation, Assert.Single(result.Questions).Prompt);
    }

    [Fact]
    public void A_blank_id_is_an_error()
    {
        var result = QuestionImportParser.Parse(Sheet("|Reading|Q|A1|A2|||A|||"));

        Assert.Equal("id", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void A_duplicate_id_in_one_file_is_an_error_naming_the_first_row()
    {
        var result = QuestionImportParser.Parse(Sheet(
            "R01|Reading|First|A1|A2|||A|||",
            "r01|Reading|Second|A1|A2|||A|||"));   // case-insensitive: both become R01

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Row);
        Assert.Equal("id", error.Column);
        Assert.Contains("2", error.Message);       // points back at the first occurrence
    }

    [Fact]
    public void An_id_is_normalised_to_uppercase()
    {
        // The database index is case-sensitive; normalising here is what stops "L01" and "l01"
        // becoming two questions across two imports.
        var result = QuestionImportParser.Parse(Sheet("r01|Reading|Q|A1|A2|||A|||"));

        Assert.Equal("R01", Assert.Single(result.Questions).ExternalId);
    }

    [Fact]
    public void A_passage_id_is_resolved_to_its_text()
    {
        // PassageRef holds the FULL passage text, so the sheet's job is to stop the author
        // retyping an 800-character passage once per question.
        var passage = new PassageRow(2, "P1", "The Industrial Revolution began…");
        var result = QuestionImportParser.Parse(Sheet([passage],
            "R01|Reading|Q1|A1|A2|||A|P1||",
            "R02|Reading|Q2|A1|A2|||B|P1||"));

        Assert.Empty(result.Errors);
        Assert.All(result.Questions, q => Assert.Equal("The Industrial Revolution began…", q.PassageText));
    }

    [Fact]
    public void An_unknown_passage_id_is_an_error()
    {
        var result = QuestionImportParser.Parse(Sheet("R01|Reading|Q|A1|A2|||A|P9||"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("passage_id", error.Column);
        Assert.Contains("P9", error.Message);
    }

    [Fact]
    public void An_invalid_audio_filename_is_an_error()
    {
        var result = QuestionImportParser.Parse(Sheet("L01|Listening|Q|A1|A2|||A||...|"));

        Assert.Equal("audio_file", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public void A_missing_required_column_is_one_error_not_one_per_row()
    {
        var columns = new[] { "id", "section", "prompt", "choice_a", "choice_b" };   // no "answer"
        var rows = Enumerable.Range(0, 50).Select(i => new QuestionRow(
            i + 2,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = $"R{i:00}", ["section"] = "Reading", ["prompt"] = "Q",
                ["choice_a"] = "A1", ["choice_b"] = "A2",
            })).ToList();

        var result = QuestionImportParser.Parse(new ImportWorkbook(columns, rows, []));

        var error = Assert.Single(result.Errors);
        Assert.Equal("answer", error.Column);
        Assert.Empty(result.Questions);
    }

    [Fact]
    public void A_trailing_blank_row_is_skipped_not_reported()
    {
        // Excel leaves these behind constantly; reporting them would bury the real errors.
        var result = QuestionImportParser.Parse(Sheet(Good, "||||||||||"));

        Assert.Empty(result.Errors);
        Assert.Single(result.Questions);
    }

    [Fact]
    public void Every_bad_row_is_reported_in_one_pass()
    {
        // The author fixes the sheet once and re-uploads. Reporting only the first error would
        // make that a 140-round trip.
        var result = QuestionImportParser.Parse(Sheet(
            Good,
            "L02|Speaking|Q|A1|A2|||A|||",
            "L03|Listening|Q|A1|A2|||Z|||",
            "L04|Listening||A1|A2|||A|||"));

        Assert.Equal(3, result.Errors.Count);
        Assert.Single(result.Questions);           // only the good row survives
    }

    [Fact]
    public void Tags_are_split_trimmed_and_emptied_cleanly()
    {
        var result = QuestionImportParser.Parse(Sheet("R01|Reading|Q|A1|A2|||A|||easy, itp ,, main-idea"));

        Assert.Equal(new[] { "easy", "itp", "main-idea" }, Assert.Single(result.Questions).Tags);
    }
}
