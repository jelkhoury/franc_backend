using ClosedXML.Excel;
using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FrancProject.Services
{
    public class GameQuestionImportService : IGameQuestionImportService
    {
        private static readonly Regex LevelSheetRegex = new(@"^Level\s*(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex OptionPrefixRegex = new(@"^\s*[A-Da-d]\s*[\.\)]\s*", RegexOptions.Compiled);

        /// <summary>First row of first question block (rows 1–4 = title/headers, 5 = headers, 6 blank).</summary>
        private const int FirstQuestionRow = 7;

        private readonly DataContext _context;
        private readonly ILogger<GameQuestionImportService> _logger;

        public GameQuestionImportService(DataContext context, ILogger<GameQuestionImportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<GameQuestionImportResultDto> ImportFromExcelAsync(Stream excelStream,
            CancellationToken cancellationToken = default)
        {
            var result = new GameQuestionImportResultDto();

            if (excelStream == null || !excelStream.CanRead)
            {
                result.Errors.Add("Excel stream is missing or not readable.");
                return result;
            }

            Dictionary<int, long> levelIds;
            try
            {
                levelIds = await _context.GameLevels
                    .AsNoTracking()
                    .Where(l => l.IsActive)
                    .ToDictionaryAsync(l => l.LevelNumber, l => l.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load game levels.");
                result.Errors.Add("Could not load game levels from the database.");
                return result;
            }

            for (var i = 1; i <= 5; i++)
                result.ImportedPerLevel[i] = 0;

            using var workbook = new XLWorkbook(excelStream);

            foreach (var worksheet in workbook.Worksheets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var match = LevelSheetRegex.Match(worksheet.Name);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var levelNumber) ||
                    levelNumber is < 1 or > 5)
                {
                    result.SkippedBlocks++;
                    _logger.LogInformation("Skipping worksheet '{Name}' (not Level 1–5).", worksheet.Name);
                    continue;
                }

                if (!levelIds.TryGetValue(levelNumber, out var levelId))
                {
                    var msg = $"Sheet '{worksheet.Name}': level {levelNumber} not found or inactive in database.";
                    result.Errors.Add(msg);
                    _logger.LogWarning(msg);
                    continue;
                }

                var sheetQuestions = new List<GameQuestion>();
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                if (lastRow < FirstQuestionRow + 3)
                {
                    result.Errors.Add($"Sheet '{worksheet.Name}': no question rows (expected data from row {FirstQuestionRow}).");
                    continue;
                }

                var level5DetectionFailures = 0;

                for (var r = FirstQuestionRow; r <= lastRow - 3; r += 4)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TryGetNumericQuestionNumber(worksheet.Cell(r, 1), out _))
                    {
                        result.SkippedBlocks++;
                        continue;
                    }

                    if (!TryParseQuestionBlock(worksheet, r, out var parsed, out var failReason,
                            out var failedOnlyOnCorrectDetection))
                    {
                        result.FailedBlocks++;
                        result.Errors.Add($"Sheet '{worksheet.Name}', row {r}: {failReason}");
                        _logger.LogWarning("Import block failed at sheet {Sheet} row {Row}: {Reason}", worksheet.Name, r,
                            failReason);

                        if (levelNumber == 5 && failedOnlyOnCorrectDetection)
                            level5DetectionFailures++;

                        continue;
                    }

                    sheetQuestions.Add(new GameQuestion
                    {
                        LevelId = levelId,
                        QuestionText = parsed!.QuestionText,
                        OptionA = parsed.OptionA,
                        OptionB = parsed.OptionB,
                        OptionC = parsed.OptionC,
                        OptionD = parsed.OptionD,
                        CorrectOption = parsed.CorrectOption,
                        Hint = parsed.Hint,
                        IsActive = true
                    });
                }

                if (levelNumber == 5 && level5DetectionFailures >= 3)
                {
                    result.Errors.Add(
                        "Level 5: multiple question blocks have no detectable correct-answer formatting in column D " +
                        "(expected one option with different font color than the other three, or exactly one bold option). " +
                        "Without that, the importer cannot determine the correct answer.");
                }

                if (sheetQuestions.Count == 0)
                {
                    result.Errors.Add($"Sheet '{worksheet.Name}': no valid question blocks found.");
                    continue;
                }

                try
                {
                    await ReplaceQuestionsForLevelAsync(levelId, sheetQuestions, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist questions for level {LevelNumber}.", levelNumber);
                    result.Errors.Add($"Sheet '{worksheet.Name}': failed to save ({ex.Message}).");
                    result.FailedBlocks += sheetQuestions.Count;
                    continue;
                }

                result.ImportedPerLevel[levelNumber] = sheetQuestions.Count;
                result.TotalImported += sheetQuestions.Count;
                _logger.LogInformation("Imported {Count} questions for level {LevelNumber} from sheet '{Sheet}'.",
                    sheetQuestions.Count, levelNumber, worksheet.Name);
            }

            return result;
        }

        private async Task ReplaceQuestionsForLevelAsync(long levelId, List<GameQuestion> newQuestions,
            CancellationToken cancellationToken)
        {
            // No user transactions: compatible with SqlServerRetryingExecutionStrategy. Replace is two-phase SaveChanges.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                try
                {
                    var existing = await _context.GameQuestions
                        .Where(q => q.LevelId == levelId)
                        .ToListAsync(cancellationToken);

                    _context.GameQuestions.RemoveRange(existing);
                    await _context.SaveChangesAsync(cancellationToken);

                    foreach (var q in newQuestions)
                        _context.GameQuestions.Add(q);

                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex,
                        "Hard delete of questions for level {LevelId} failed (likely FK to session answers). Deactivating old rows.",
                        levelId);

                    _context.ChangeTracker.Clear();

                    var existing2 = await _context.GameQuestions
                        .Where(q => q.LevelId == levelId)
                        .ToListAsync(cancellationToken);

                    foreach (var q in existing2)
                        q.IsActive = false;

                    await _context.SaveChangesAsync(cancellationToken);

                    foreach (var q in newQuestions)
                        _context.GameQuestions.Add(q);

                    await _context.SaveChangesAsync(cancellationToken);
                }
            });
        }

        /// <summary>Column A must be a positive integer question index (Excel number or text).</summary>
        private static bool TryGetNumericQuestionNumber(IXLCell cell, out int number)
        {
            number = 0;
            if (cell.IsEmpty())
                return false;

            if (cell.TryGetValue(out double dbl) && dbl >= 1 && dbl <= 10_000 && Math.Abs(dbl - Math.Round(dbl)) < 0.0001)
            {
                number = (int)Math.Round(dbl);
                return true;
            }

            var t = CellText(cell);
            return int.TryParse(t, out number) && number >= 1;
        }

        private static bool TryParseQuestionBlock(IXLWorksheet sheet, int startRow,
            out ParsedBlock? parsed, out string failReason, out bool failedOnlyOnCorrectDetection)
        {
            parsed = null;
            failedOnlyOnCorrectDetection = false;

            var qText = CellText(sheet.Cell(startRow, 2));
            var dA = CellText(sheet.Cell(startRow, 4));
            var dB = CellText(sheet.Cell(startRow + 1, 4));
            var dC = CellText(sheet.Cell(startRow + 2, 4));
            var dD = CellText(sheet.Cell(startRow + 3, 4));

            if (string.IsNullOrEmpty(qText))
            {
                failReason = "Question text (column B) is empty.";
                return false;
            }

            if (string.IsNullOrEmpty(dA) || string.IsNullOrEmpty(dB) || string.IsNullOrEmpty(dC) ||
                string.IsNullOrEmpty(dD))
            {
                failReason = "One or more options in column D are empty (expected 4 rows).";
                return false;
            }

            var optionCells = new[]
            {
                sheet.Cell(startRow, 4),
                sheet.Cell(startRow + 1, 4),
                sheet.Cell(startRow + 2, 4),
                sheet.Cell(startRow + 3, 4)
            };

            if (!TryDetectCorrectOption(optionCells, out var correctLetter, out var detectError))
            {
                failedOnlyOnCorrectDetection = true;
                failReason = detectError ?? "Could not detect exactly one correct option from cell formatting.";
                return false;
            }

            var hintRaw = CellText(sheet.Cell(startRow, 8));
            string? hint = string.IsNullOrEmpty(hintRaw) ? null : hintRaw;

            parsed = new ParsedBlock
            {
                QuestionText = qText.Trim(),
                OptionA = NormalizeOptionText(dA),
                OptionB = NormalizeOptionText(dB),
                OptionC = NormalizeOptionText(dC),
                OptionD = NormalizeOptionText(dD),
                CorrectOption = correctLetter,
                Hint = hint
            };

            if (string.IsNullOrWhiteSpace(parsed.OptionA) || string.IsNullOrWhiteSpace(parsed.OptionB) ||
                string.IsNullOrWhiteSpace(parsed.OptionC) || string.IsNullOrWhiteSpace(parsed.OptionD))
            {
                failReason = "An option became empty after normalization.";
                failedOnlyOnCorrectDetection = false;
                parsed = null;
                return false;
            }

            failReason = "";
            return true;
        }

        private static string CellText(IXLCell cell)
        {
            try
            {
                if (cell.IsEmpty())
                    return string.Empty;
                return cell.GetString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeOptionText(string raw)
        {
            var s = raw.Trim();
            if (s.Length == 0)
                return s;
            s = OptionPrefixRegex.Replace(s, "");
            return s.Trim();
        }

        /// <summary>
        /// Detects the correct option as the one whose font formatting differs from the other three
        /// (same signature on three cells, one outlier). Falls back to exactly one bold cell.
        /// </summary>
        private static bool TryDetectCorrectOption(IXLCell[] cells, out string correctLetter, out string? error)
        {
            correctLetter = "";
            error = null;

            if (cells.Length != 4)
            {
                error = "Internal: expected 4 option cells.";
                return false;
            }

            var keys = cells.Select(GetFontFormattingKey).ToArray();
            var byKey = keys.Select((k, i) => (k, i)).GroupBy(t => t.k).OrderByDescending(g => g.Count()).ToList();

            if (byKey.Count == 2 && byKey[0].Count() == 3 && byKey[1].Count() == 1)
            {
                correctLetter = LetterForIndex(byKey[1].First().i);
                return true;
            }

            if (byKey.Count == 2 && byKey[0].Count() == 1 && byKey[1].Count() == 3)
            {
                correctLetter = LetterForIndex(byKey[0].First().i);
                return true;
            }

            var boldFlags = cells.Select(c =>
            {
                try
                {
                    return c.Style.Font.Bold;
                }
                catch
                {
                    return false;
                }
            }).ToArray();

            if (boldFlags.Count(b => b) == 1)
            {
                correctLetter = LetterForIndex(Array.IndexOf(boldFlags, true));
                return true;
            }

            error =
                "Could not detect exactly one correct option: need three identical font styles and one different in column D, or exactly one bold option.";
            return false;
        }

        private static string LetterForIndex(int index) => index switch
        {
            0 => "A",
            1 => "B",
            2 => "C",
            3 => "D",
            _ => "A"
        };

        private static string GetFontFormattingKey(IXLCell cell)
        {
            try
            {
                var font = cell.Style.Font;
                var fc = font.FontColor;
                var colorPart = fc.ColorType switch
                {
                    XLColorType.Color => $"argb:{fc.Color.ToArgb()}",
                    XLColorType.Indexed => $"idx:{fc.Indexed}",
                    XLColorType.Theme => $"theme:{(int)fc.ThemeColor}:{fc.ThemeTint:F4}",
                    _ => "default"
                };
                return $"{colorPart}|b:{font.Bold}|i:{font.Italic}|u:{font.Underline}";
            }
            catch
            {
                return "unknown";
            }
        }

        private sealed class ParsedBlock
        {
            public string QuestionText { get; set; } = null!;
            public string OptionA { get; set; } = null!;
            public string OptionB { get; set; } = null!;
            public string OptionC { get; set; } = null!;
            public string OptionD { get; set; } = null!;
            public string CorrectOption { get; set; } = null!;
            public string? Hint { get; set; }
        }
    }
}
