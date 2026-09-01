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
                "Berkas tidak dapat dibaca sebagai Excel (.xlsx). Gunakan template yang disediakan.", 400);
        }

        using (workbook)
        {
            var sheet = Find(workbook, QuestionsSheet)
                ?? throw new AssessmentException(
                    $"Sheet '{QuestionsSheet}' tidak ditemukan. Gunakan template yang disediakan.", 400);

            var header = sheet.FirstRowUsed()
                ?? throw new AssessmentException($"Sheet '{QuestionsSheet}' kosong.", 400);

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
            "CARA MENGISI TEMPLATE BANK SOAL INVERTA",
            "",
            "1. Isi sheet 'Questions'. Satu baris = satu soal. Jangan mengubah baris judul (baris 1).",
            "",
            "2. Kolom wajib: " + string.Join(", ", QuestionImportParser.RequiredColumns) + ".",
            "   Kolom opsional: " + string.Join(", ", QuestionImportParser.OptionalColumns) + ".",
            "",
            "3. id — nomor soal buatan Anda, misalnya L01, S14, R07. Harus unik dalam satu berkas.",
            "   Id ini dipakai untuk memperbarui soal: mengunggah ulang berkas yang sama akan",
            "   MEMPERBAIKI soal yang sudah ada, bukan menggandakannya.",
            "",
            "4. section — salah satu dari: " + string.Join(", ", Enum.GetNames<Academy.Domain.Enums.QuestionSection>()) + ".",
            "",
            "5. answer — SATU huruf saja: A, B, C, atau D. Bukan angka, dan tidak boleh lebih dari satu",
            "   huruf. Setiap soal TOEFL ITP hanya punya satu jawaban benar.",
            "",
            "6. Pilihan jawaban diisi berurutan mulai choice_a. Minimal dua pilihan. Jangan",
            "   mengosongkan choice_b lalu mengisi choice_c.",
            "",
            "7. prompt boleh berisi beberapa baris (tekan Alt+Enter di dalam sel). Percakapan",
            "   Listening biasanya tiga baris: dua pembicara dan pertanyaannya.",
            "",
            "8. passage_id — untuk soal Reading yang berbagi satu bacaan. Tulis bacaannya SEKALI di",
            "   sheet 'Passages', lalu tulis id-nya di setiap soal yang memakainya.",
            "",
            "9. audio_file — cukup nama berkasnya, misalnya L01.mp3. Berkas audionya diunggah",
            "   terpisah di halaman impor. Urutannya bebas: impor dulu atau unggah audio dulu.",
            "",
            "10. tags — dipisah koma, misalnya: easy, main-idea.",
            "",
            "11. Kolom opsional yang dikosongkan TIDAK menghapus data lama saat memperbarui soal.",
            "    Untuk mengosongkan audio atau bacaan, gunakan halaman Bank Soal.",
            "",
            "12. Berkas diperiksa seluruhnya sebelum disimpan. Jika ada satu kesalahan, TIDAK ADA",
            "    soal yang tersimpan — perbaiki lalu unggah ulang berkas yang sama.",
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
