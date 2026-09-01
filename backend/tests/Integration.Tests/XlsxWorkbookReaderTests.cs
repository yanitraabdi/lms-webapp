using Academy.Application.Assessments;
using Academy.Infrastructure.Assessments;
using ClosedXML.Excel;

namespace Academy.Integration.Tests;

/// <summary>
/// These live in Integration.Tests only because Application.Tests cannot see ClosedXML — nothing
/// here needs a database or a running API.
///
/// The load-bearing test is the round trip: the template we hand the author must itself parse
/// clean. That is what stops the template and the parser drifting apart, which is the failure an
/// author would experience as "I filled in your own file and it says the columns are wrong".
/// </summary>
public class XlsxWorkbookReaderTests
{
    private static MemoryStream Book(Action<IXLWorkbook> build)
    {
        using var wb = new XLWorkbook();
        build(wb);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void The_template_parses_with_no_errors()
    {
        using var stream = new MemoryStream(XlsxWorkbookReader.BuildTemplate());

        var result = QuestionImportParser.Parse(XlsxWorkbookReader.Read(stream));

        Assert.Empty(result.Errors);
        Assert.NotEmpty(result.Questions);          // the worked examples are real, valid rows
    }

    [Fact]
    public void The_template_carries_every_column_the_parser_requires()
    {
        using var stream = new MemoryStream(XlsxWorkbookReader.BuildTemplate());

        var workbook = XlsxWorkbookReader.Read(stream);

        foreach (var column in QuestionImportParser.RequiredColumns)
            Assert.Contains(column, workbook.Columns);
        foreach (var column in QuestionImportParser.OptionalColumns)
            Assert.Contains(column, workbook.Columns);
    }

    [Fact]
    public void The_template_has_an_instructions_sheet()
    {
        // The author never gets a separate document, so the sheet has to carry the rules.
        using var wb = new XLWorkbook(new MemoryStream(XlsxWorkbookReader.BuildTemplate()));

        Assert.Contains(wb.Worksheets, w => string.Equals(w.Name, "Instructions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Headers_are_matched_case_and_space_insensitively()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = " ID ";
            ws.Cell(1, 2).Value = "Section";
            ws.Cell(2, 1).Value = "R01";
            ws.Cell(2, 2).Value = "Reading";
        });

        var workbook = XlsxWorkbookReader.Read(stream);

        Assert.Equal(new[] { "id", "section" }, workbook.Columns);
        Assert.Equal("R01", workbook.Rows[0].Cells["id"]);
    }

    [Fact]
    public void A_row_number_is_the_real_sheet_row()
    {
        // The error messages send the author back to a row in Excel; an off-by-one here makes
        // every message point at the wrong line.
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = "id";
            ws.Cell(2, 1).Value = "R01";
            ws.Cell(3, 1).Value = "R02";
        });

        var workbook = XlsxWorkbookReader.Read(stream);

        Assert.Equal(new[] { 2, 3 }, workbook.Rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void A_line_break_inside_a_cell_survives_the_read()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = "prompt";
            ws.Cell(2, 1).Value = "M: Are you going?\nW: I can't.";
        });

        var workbook = XlsxWorkbookReader.Read(stream);

        Assert.Contains("\n", workbook.Rows[0].Cells["prompt"]);
    }

    [Fact]
    public void The_passages_sheet_is_read_by_header_name_not_position()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = "id";
            ws.Cell(2, 1).Value = "R01";

            var ps = wb.AddWorksheet("Passages");
            ps.Cell(1, 1).Value = "notes";          // an author inserted a column
            ps.Cell(1, 2).Value = "passage_id";
            ps.Cell(1, 3).Value = "text";
            ps.Cell(2, 1).Value = "ignore me";
            ps.Cell(2, 2).Value = "P1";
            ps.Cell(2, 3).Value = "The passage.";
        });

        var workbook = XlsxWorkbookReader.Read(stream);

        var passage = Assert.Single(workbook.Passages);
        Assert.Equal("P1", passage.PassageId);
        Assert.Equal("The passage.", passage.Text);
    }

    [Fact]
    public void A_workbook_with_no_passages_sheet_reads_fine()
    {
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = "id";
            ws.Cell(2, 1).Value = "L01";
        });

        Assert.Empty(XlsxWorkbookReader.Read(stream).Passages);
    }

    [Fact]
    public void A_workbook_with_no_questions_sheet_is_refused_with_an_admin_facing_message()
    {
        using var stream = Book(wb => wb.AddWorksheet("Sheet1").Cell(1, 1).Value = "hello");

        var error = Assert.Throws<AssessmentException>(() => XlsxWorkbookReader.Read(stream));
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("Questions", error.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_workbook_is_refused_not_crashed()
    {
        using var garbage = new MemoryStream("this is not a spreadsheet"u8.ToArray());

        var error = Assert.Throws<AssessmentException>(() => XlsxWorkbookReader.Read(garbage));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void An_id_column_formatted_as_text_keeps_its_leading_zero()
    {
        // Excel reads "01" as the number 1 unless the column is text-formatted. The template does
        // that formatting; this proves the reader honours it rather than re-coercing.
        using var stream = Book(wb =>
        {
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = "id";
            ws.Column(1).Style.NumberFormat.Format = "@";
            ws.Cell(2, 1).SetValue("01");
        });

        Assert.Equal("01", XlsxWorkbookReader.Read(stream).Rows[0].Cells["id"]);
    }
}
