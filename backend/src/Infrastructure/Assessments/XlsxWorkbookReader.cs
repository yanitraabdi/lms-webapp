using Academy.Application.Assessments;
using ClosedXML.Excel;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// The only file that knows about ClosedXML. It reduces a workbook to strings so the validation
/// rules can live in Application, where they are testable without a file.
///
/// Every cell is read as its FORMATTED string and trimmed, never through its cell type: an id like
/// "01" is a number to Excel and would come back as 1, and a lone "2" in the answer column must
/// stay a "2" the parser can reject rather than an int it might trust. The template defends the id
/// column further by formatting it as text.
/// </summary>
public static class XlsxWorkbookReader
{
    private const string QuestionsSheet = "Questions";
    private const string PassagesSheet = "Passages";
    private const string InstructionsSheet = "Instructions";

    public static ImportWorkbook Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception e) when (e is not AssessmentException)
        {
            // A corrupt or non-xlsx upload is the admin's mistake, not a 500.
            throw new AssessmentException(
                "The file can't be read as Excel (.xlsx). Use the provided template.", 400);
        }

        using (workbook)
        {
            var sheet = Find(workbook, QuestionsSheet)
                ?? throw new AssessmentException(
                    $"Sheet '{QuestionsSheet}' not found. Use the provided template.", 400);

            var header = sheet.FirstRowUsed()
                ?? throw new AssessmentException($"Sheet '{QuestionsSheet}' is empty.", 400);

            var columns = new List<string>();
            var byColumnNumber = new Dictionary<int, string>();
            foreach (var cell in header.CellsUsed())
            {
                var name = Text(cell).ToLowerInvariant();
                if (name.Length == 0 || byColumnNumber.ContainsValue(name)) continue;
                columns.Add(name);
                byColumnNumber[cell.Address.ColumnNumber] = name;
            }

            var rows = new List<QuestionRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
            for (var r = header.RowNumber() + 1; r <= lastRow; r++)
            {
                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (column, name) in byColumnNumber)
                    cells[name] = Text(sheet.Cell(r, column));
                rows.Add(new QuestionRow(r, cells));
            }

            return new ImportWorkbook(columns, rows, ReadPassages(workbook));
        }
    }

    private static List<PassageRow> ReadPassages(XLWorkbook workbook)
    {
        var passages = new List<PassageRow>();

        var sheet = Find(workbook, PassagesSheet);
        var header = sheet?.FirstRowUsed();
        if (sheet is null || header is null) return passages;

        // By header name, not position: an author who inserts a notes column should not silently
        // shift every passage into the wrong field.
        int? idColumn = null, textColumn = null;
        foreach (var cell in header.CellsUsed())
        {
            var name = Text(cell).ToLowerInvariant();
            if (name == "passage_id") idColumn = cell.Address.ColumnNumber;
            else if (name == "text") textColumn = cell.Address.ColumnNumber;
        }
        if (idColumn is null || textColumn is null) return passages;

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
        for (var r = header.RowNumber() + 1; r <= lastRow; r++)
        {
            var id = Text(sheet.Cell(r, idColumn.Value));
            var text = Text(sheet.Cell(r, textColumn.Value));
            if (id.Length == 0 && text.Length == 0) continue;
            passages.Add(new PassageRow(r, id, text));
        }

        return passages;
    }

    // ---------------------------------------------------------------- template

    /// <summary>
    /// The workbook the author starts from. Built from the parser's OWN column lists, so a rule
    /// change cannot leave the template describing a format the parser rejects.
    /// </summary>
    public static byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();

        var questions = workbook.AddWorksheet(QuestionsSheet);
        var columns = QuestionImportParser.RequiredColumns
            .Concat(QuestionImportParser.OptionalColumns)
            .ToList();

        for (var c = 0; c < columns.Count; c++)
        {
            questions.Cell(1, c + 1).Value = columns[c];
            questions.Cell(1, c + 1).Style.Font.Bold = true;
        }

        // Text format on id: otherwise Excel reads "01" as the number 1 and the author's careful
        // numbering silently collapses.
        questions.Column(columns.IndexOf("id") + 1).Style.NumberFormat.Format = "@";
        questions.Column(columns.IndexOf("prompt") + 1).Width = 60;
        questions.Column(columns.IndexOf("prompt") + 1).Style.Alignment.WrapText = true;

        // One worked example per exam section. These are valid rows: the template round-trips
        // through the parser with no errors, which is asserted in XlsxWorkbookReaderTests.
        var examples = new[]
        {
            new Dictionary<string, string>
            {
                ["id"] = "L01", ["section"] = "Listening",
                ["prompt"] = "M: Have you finished the assignment?\nW: I still have two pages left.\nWhat does the woman mean?",
                ["choice_a"] = "She has finished.", ["choice_b"] = "She is almost done.",
                ["choice_c"] = "She has not started.", ["choice_d"] = "She lost the assignment.",
                ["answer"] = "B", ["audio_file"] = "L01.mp3", ["tags"] = "conversation",
            },
            new Dictionary<string, string>
            {
                ["id"] = "S01", ["section"] = "Structure",
                ["prompt"] = "The committee ____ its decision yesterday.",
                ["choice_a"] = "announce", ["choice_b"] = "announcing",
                ["choice_c"] = "announced", ["choice_d"] = "to announce",
                ["answer"] = "C", ["tags"] = "tense",
            },
            new Dictionary<string, string>
            {
                ["id"] = "R01", ["section"] = "Reading",
                ["prompt"] = "What is the main idea of the passage?",
                ["choice_a"] = "Trade routes shaped early cities.",
                ["choice_b"] = "Cities grew only near rivers.",
                ["choice_c"] = "Farming was unimportant.",
                ["choice_d"] = "Trade declined over time.",
                ["answer"] = "A", ["passage_id"] = "P1", ["tags"] = "main-idea",
            },
        };

        for (var r = 0; r < examples.Length; r++)
            for (var c = 0; c < columns.Count; c++)
                if (examples[r].TryGetValue(columns[c], out var value))
                    questions.Cell(r + 2, c + 1).SetValue(value);

        var passages = workbook.AddWorksheet(PassagesSheet);
        passages.Cell(1, 1).Value = "passage_id";
        passages.Cell(1, 2).Value = "text";
        passages.Row(1).Style.Font.Bold = true;
        passages.Column(2).Width = 90;
        passages.Column(2).Style.Alignment.WrapText = true;
        passages.Cell(2, 1).SetValue("P1");
        passages.Cell(2, 2).SetValue(
            "Early cities rarely grew in isolation. Where trade routes met, surplus grain and " +
            "craft goods changed hands, and the settlements that controlled those crossings grew " +
            "faster than their neighbours.");

        var instructions = workbook.AddWorksheet(InstructionsSheet);
        instructions.Column(1).Width = 110;
        instructions.Column(1).Style.Alignment.WrapText = true;
        var lines = new[]
        {
            "HOW TO FILL IN THE INVERTA QUESTION BANK TEMPLATE",
            "",
            "1. Fill in the 'Questions' sheet. One row = one question. Do not change the header row (row 1).",
            "",
            "2. Required columns: " + string.Join(", ", QuestionImportParser.RequiredColumns) + ".",
            "   Optional columns: " + string.Join(", ", QuestionImportParser.OptionalColumns) + ".",
            "",
            "3. id — a question number you make up, e.g. L01, S14, R07. Must be unique within the file.",
            "   The id is used to update questions: uploading the same file again will",
            "   UPDATE the existing questions, not duplicate them.",
            "",
            "4. section — one of: " + string.Join(", ", Enum.GetNames<Academy.Domain.Enums.QuestionSection>()) + ".",
            "",
            "5. answer — exactly ONE letter: A, B, C, or D. Not a number, and not more than one",
            "   letter. Every TOEFL ITP question has only one correct answer.",
            "",
            "6. Fill in options in order starting with choice_a. At least two options. Do not",
            "   leave choice_b empty and then fill in choice_c.",
            "",
            "7. prompt may contain several lines (press Alt+Enter inside the cell). A",
            "   Listening conversation is usually three lines: two speakers and the question.",
            "",
            "8. passage_id — for Reading questions that share one passage. Write the passage ONCE in the",
            "   'Passages' sheet, then write its id on every question that uses it.",
            "",
            "9. audio_file — just the file name, e.g. L01.mp3. The audio files are uploaded",
            "   separately on the import page. Order doesn't matter: import first or upload audio first.",
            "",
            "10. tags — comma-separated, e.g. easy, main-idea.",
            "",
            "11. An optional column left empty does NOT erase existing data when updating a question.",
            "    To clear audio or a passage, use the Question bank page.",
            "",
            "12. The whole file is checked before anything is saved. If there is a single error, NO",
            "    questions are saved — fix it and upload the same file again.",
        };
        for (var i = 0; i < lines.Length; i++)
            instructions.Cell(i + 1, 1).SetValue(lines[i]);
        instructions.Cell(1, 1).Style.Font.Bold = true;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static IXLWorksheet? Find(XLWorkbook workbook, string name)
        => workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();
}
