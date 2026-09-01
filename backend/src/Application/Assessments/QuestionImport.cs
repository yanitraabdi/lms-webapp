using System.Text.RegularExpressions;
using Academy.Domain.Enums;

namespace Academy.Application.Assessments;

/// <summary>
/// Deterministic storage keys for listening audio: "L01.mp3" becomes "audio/l01.mp3", computed the
/// same way whether the caller has the uploaded file or only the filename written in an import
/// sheet. That is what lets the two steps happen in either order, with no filename-to-key mapping
/// table to keep in sync.
///
/// The result is a trust boundary — it goes into a URL that the media route hands to storage — so
/// it is built from an allow-list, forced to start with a letter or digit (which kills every
/// "." / ".." / leading-dot trick), and capped well inside LocalObjectStorage's 200-character
/// limit. Anything that does not survive that returns null rather than a guessed key.
/// </summary>
public static partial class AudioKey
{
    private const int MaxBasenameLength = 100;

    /// <summary>The key for a stored object, or null when the basename sanitises to nothing usable.</summary>
    public static string? Build(string? basename, string extension)
    {
        if (basename is null || !ExtensionPattern().IsMatch(extension)) return null;

        var cleaned = new string(basename
            .ToLowerInvariant()
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            .ToArray());

        // Must START with a letter or digit: that is what makes "..", ".", and "---" impossible,
        // and it is a stronger rule than trimming, which "..." would survive as "".
        if (cleaned.Length == 0 || !char.IsAsciiLetterOrDigit(cleaned[0])) return null;
        if (cleaned.Length > MaxBasenameLength) cleaned = cleaned[..MaxBasenameLength];

        return $"audio/{cleaned}{extension}";
    }

    /// <summary>
    /// The key for a filename written in an import sheet. Only the basename is taken — any path
    /// segments a spreadsheet may carry are dropped, not resolved.
    /// </summary>
    public static string? FromFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;

        // Split on both separators by hand: Path.GetFileName does not treat '\' as a separator on
        // Linux, and a sheet authored on Windows can carry either.
        var name = filename.Trim();
        var cut = name.LastIndexOfAny(['/', '\\']);
        if (cut >= 0) name = name[(cut + 1)..];

        var dot = name.LastIndexOf('.');
        if (dot <= 0) return null;                       // no extension, or a leading-dot name

        return Build(name[..dot], name[dot..].ToLowerInvariant());
    }

    /// <summary>
    /// The key for an UPLOADED file. Only the basename comes from the client's filename; the
    /// extension is supplied by the caller from the content type, keeping the existing
    /// MediaUpload rule that a client filename never decides what a stored object is.
    /// </summary>
    public static string? ForUpload(string? filename, string extension)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;

        var name = filename.Trim();
        var cut = name.LastIndexOfAny(['/', '\\']);
        if (cut >= 0) name = name[(cut + 1)..];

        var dot = name.LastIndexOf('.');
        return Build(dot > 0 ? name[..dot] : name, extension);
    }

    [GeneratedRegex(@"\A\.[a-z0-9]{1,6}\z")]
    private static partial Regex ExtensionPattern();
}

// ---------------------------------------------------------------- workbook shape

/// <summary>One row of the Questions sheet, every cell already read as a trimmed string.</summary>
public record QuestionRow(int RowNumber, IReadOnlyDictionary<string, string> Cells);

/// <summary>One row of the Passages sheet. A passage serves ~10 Reading questions.</summary>
public record PassageRow(int RowNumber, string PassageId, string Text);

/// <summary>
/// A workbook reduced to strings. The parser takes THIS, not a file, so every rule below is
/// testable without ClosedXML, a temp file, or a database.
/// </summary>
public record ImportWorkbook(
    IReadOnlyList<string> Columns,
    IReadOnlyList<QuestionRow> Rows,
    IReadOnlyList<PassageRow> Passages);

// ---------------------------------------------------------------- parse output

/// <summary>An admin-facing problem, naming the cell it came from. Row 1 is the header.</summary>
public record ImportError(int Row, string Column, string Message);

/// <summary>A validated row, ready to become a <c>Question</c>. Passage text is already resolved.</summary>
public record ParsedQuestion(
    string ExternalId,
    QuestionSection Section,
    string Prompt,
    IReadOnlyList<string> Choices,
    int CorrectIndex,
    string? PassageText,
    string? AudioRef,
    IReadOnlyList<string> Tags);

public record ImportParseResult(
    IReadOnlyList<ParsedQuestion> Questions,
    IReadOnlyList<ImportError> Errors);

// ---------------------------------------------------------------- the parser

/// <summary>
/// Validates a transcribed workbook. Pure: no file, no database, no clock.
///
/// Two properties matter more than any individual rule. Every row is checked even after one
/// fails, because the author fixes the sheet once and re-uploads — reporting the first error only
/// would turn a 140-question bank into 140 round trips. And a row that produced an error is never
/// emitted, so the caller can commit on "no errors" alone.
/// </summary>
public static class QuestionImportParser
{
    public static readonly string[] RequiredColumns =
        ["id", "section", "prompt", "choice_a", "choice_b", "answer"];

    public static readonly string[] OptionalColumns =
        ["choice_c", "choice_d", "passage_id", "audio_file", "tags"];

    private static readonly string[] ChoiceColumns = ["choice_a", "choice_b", "choice_c", "choice_d"];

    private const int MaxExternalIdLength = 64;

    public static ImportParseResult Parse(ImportWorkbook workbook)
    {
        var errors = new List<ImportError>();

        var present = new HashSet<string>(workbook.Columns, StringComparer.OrdinalIgnoreCase);
        foreach (var column in RequiredColumns)
            if (!present.Contains(column))
                errors.Add(new ImportError(1, column,
                    $"Kolom wajib '{column}' tidak ada pada sheet Questions. Gunakan template yang disediakan."));

        // A missing column fails every row for one reason. Report it once, at the header.
        if (errors.Count > 0) return new ImportParseResult([], errors);

        var passages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var blankPassages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var passage in workbook.Passages)
        {
            if (passage.PassageId.Length == 0) continue;

            if (passage.Text.Length == 0)
            {
                // Many questions may reference this one id — report it once, here, against the
                // Passages row, not once per referencing question (same precedent as the missing
                // required column above).
                blankPassages.Add(passage.PassageId);
                errors.Add(new ImportError(passage.RowNumber, "text",
                    $"Passage '{passage.PassageId}' pada sheet Passages tidak memiliki teks. " +
                    "Isi teks passage-nya, atau hapus barisnya jika belum siap."));
                continue;
            }

            passages[passage.PassageId] = passage.Text;
        }

        var questions = new List<ParsedQuestion>();
        var seenIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in workbook.Rows)
        {
            // Excel leaves trailing blank rows behind; they are not the author's mistake.
            if (row.Cells.Values.All(string.IsNullOrWhiteSpace)) continue;

            var errorsBefore = errors.Count;

            var externalId = ParseId(row, seenIds, errors);
            var section = ParseSection(row, errors);
            var prompt = ParsePrompt(row, errors);
            var choicesErrorsBefore = errors.Count;
            var choices = ParseChoices(row, errors);
            var choicesValid = errors.Count == choicesErrorsBefore;
            var correctIndex = ParseAnswer(row, choices, choicesValid, errors);
            var passageText = ParsePassage(row, passages, blankPassages, errors);
            var audioRef = ParseAudio(row, errors);
            var tags = Cell(row, "tags")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (errors.Count == errorsBefore)
                questions.Add(new ParsedQuestion(
                    externalId, section, prompt, choices, correctIndex, passageText, audioRef, tags));
        }

        return new ImportParseResult(questions, errors);
    }

    // ---------------------------------------------------------------- per-column rules

    private static string ParseId(QuestionRow row, Dictionary<string, int> seen, List<ImportError> errors)
    {
        var id = Cell(row, "id");

        if (id.Length == 0)
            errors.Add(new ImportError(row.RowNumber, "id", "Kolom id wajib diisi. Contoh: L01, S14, R07."));
        else if (id.Length > MaxExternalIdLength)
            errors.Add(new ImportError(row.RowNumber, "id",
                $"Id terlalu panjang (maksimum {MaxExternalIdLength} karakter)."));
        else if (seen.TryGetValue(id, out var first))
            errors.Add(new ImportError(row.RowNumber, "id", $"Id '{id}' sudah dipakai pada baris {first}."));
        else
            seen[id] = row.RowNumber;

        // Uppercase: the sheet dedupes case-insensitively but the unique index does not.
        return id.ToUpperInvariant();
    }

    private static QuestionSection ParseSection(QuestionRow row, List<ImportError> errors)
    {
        var text = Cell(row, "section");

        // Enum.TryParse reads "0" as Listening. An author who typed a number meant no such thing,
        // and Excel turns bare digits into numbers unasked.
        var numeric = text.Length > 0 && text.All(char.IsAsciiDigit);

        if (!numeric && Enum.TryParse<QuestionSection>(text, ignoreCase: true, out var section))
            return section;

        errors.Add(new ImportError(row.RowNumber, "section",
            $"Bagian '{text}' tidak dikenal. Gunakan salah satu: {string.Join(", ", Enum.GetNames<QuestionSection>())}."));
        return QuestionSection.General;
    }

    private static string ParsePrompt(QuestionRow row, List<ImportError> errors)
    {
        var prompt = Cell(row, "prompt");
        if (prompt.Length == 0)
            errors.Add(new ImportError(row.RowNumber, "prompt", "Pertanyaan wajib diisi."));
        return prompt;
    }

    private static List<string> ParseChoices(QuestionRow row, List<ImportError> errors)
    {
        var choices = new List<string>();
        var sawBlank = false;

        foreach (var column in ChoiceColumns)
        {
            var value = Cell(row, column);
            if (value.Length == 0) { sawBlank = true; continue; }

            if (sawBlank)
            {
                // "A, blank, C" is ambiguous — does answer=C mean the third column or the second
                // surviving choice? Both readings are defensible, which is exactly why neither is
                // safe to guess on an exam.
                errors.Add(new ImportError(row.RowNumber, "choice_b",
                    "Pilihan jawaban harus berurutan mulai choice_a, tanpa kolom kosong di tengah."));
                return choices;
            }

            choices.Add(value);
        }

        if (choices.Count < 2)
            errors.Add(new ImportError(row.RowNumber, "choice_b", "Minimal 2 pilihan jawaban."));

        return choices;
    }

    private static int ParseAnswer(
        QuestionRow row, List<string> choices, bool choicesValid, List<ImportError> errors)
    {
        var answer = Cell(row, "answer").ToUpperInvariant();

        // A letter, never an index: a transcriber reliably writes "C" and unreliably writes "2"
        // for the third option. A list ("A,C") is refused because a student submits ONE int and
        // scoring is Correct.Contains(picked) — accepting it would imply a capability the runtime
        // does not have.
        if (answer.Length != 1 || answer[0] < 'A' || answer[0] > 'D')
        {
            errors.Add(new ImportError(row.RowNumber, "answer",
                "Jawaban harus satu huruf: A, B, C, atau D."));
            return -1;
        }

        var index = answer[0] - 'A';

        // When choice_a..d already failed validation (a gap, or fewer than 2 filled), that row is
        // already flagged — checking the answer against the resulting partial list would only
        // pile a second, confusing error onto the same cause.
        if (choicesValid && index >= choices.Count)
        {
            errors.Add(new ImportError(row.RowNumber, "answer",
                $"Jawaban '{answer}' menunjuk pilihan yang kosong."));
            return -1;
        }

        return index;
    }

    private static string? ParsePassage(
        QuestionRow row, Dictionary<string, string> passages, HashSet<string> blankPassages,
        List<ImportError> errors)
    {
        var passageId = Cell(row, "passage_id");
        if (passageId.Length == 0) return null;

        if (passages.TryGetValue(passageId, out var text)) return text;

        // Already reported once, against the Passages row itself — not here, and not again.
        if (blankPassages.Contains(passageId)) return null;

        errors.Add(new ImportError(row.RowNumber, "passage_id",
            $"passage_id '{passageId}' tidak ada pada sheet Passages."));
        return null;
    }

    private static string? ParseAudio(QuestionRow row, List<ImportError> errors)
    {
        var filename = Cell(row, "audio_file");
        if (filename.Length == 0) return null;

        // A referenced file that has not been uploaded yet is NOT an error — the two steps are
        // deliberately order-independent, and the listening_audio_present readiness check refuses
        // to publish until the object actually exists. Only an unusable NAME is rejected here.
        var key = AudioKey.FromFilename(filename);
        if (key is null)
            errors.Add(new ImportError(row.RowNumber, "audio_file",
                $"Nama berkas audio '{filename}' tidak valid. Gunakan nama seperti L01.mp3."));

        return key;
    }

    private static string Cell(QuestionRow row, string column)
        => row.Cells.TryGetValue(column, out var value) ? value.Trim() : "";
}
