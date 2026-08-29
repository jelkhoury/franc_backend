using ClosedXML.Excel;
using FrancProject.Dto;

namespace FrancProject.Helpers;

public static class SdsExcelExportHelper
{
    private static readonly XLColor Navy = XLColor.FromHtml("#1B365D");
    private static readonly XLColor NavySoft = XLColor.FromHtml("#2C4A73");
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1B365D");
    private static readonly XLColor AltRow = XLColor.FromHtml("#F3F6FA");
    private static readonly XLColor Line = XLColor.FromHtml("#D5DCE6");
    private static readonly XLColor TitleWhite = XLColor.FromHtml("#FFFFFF");
    private static readonly XLColor Muted = XLColor.FromHtml("#5B6777");
    private static readonly XLColor Body = XLColor.FromHtml("#1E2430");
    private static readonly XLColor CompleteFill = XLColor.FromHtml("#E5F5EC");
    private static readonly XLColor CompleteText = XLColor.FromHtml("#1F7A4D");
    private static readonly XLColor IncompleteFill = XLColor.FromHtml("#FFF4D6");
    private static readonly XLColor IncompleteText = XLColor.FromHtml("#9A6B12");
    private static readonly XLColor KpiBorder = XLColor.FromHtml("#C5CEDB");
    private static readonly XLColor HollandFill = XLColor.FromHtml("#EEF3FA");

    private const int ColCount = 8;
    private const string Dash = "—";

    public static SdsExcelExportResult BuildWorkbook(
        IReadOnlyList<SdsExportRow> rows,
        DateTime? from,
        DateTime? toInclusive,
        SdsExportCompletionFilter completion)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "Franc SDS Assessment Report";
        workbook.Properties.Author = "Franc";
        workbook.Properties.Company = "Franc";

        var ws = workbook.Worksheets.Add("SDS Attempts");
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 11;
        ws.Style.Font.FontColor = Body;
        ws.ShowGridLines = false;
        ws.SheetView.ZoomScale = 110;

        WriteBanner(ws, from, toInclusive, completion, rows);
        const int headerRow = 8;
        WriteTable(ws, headerRow, rows);
        ApplyPrintLayout(ws, headerRow, Math.Max(headerRow, headerRow + Math.Max(rows.Count, 1)));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new SdsExcelExportResult
        {
            Bytes = stream.ToArray(),
            FileName = BuildFileName(from, toInclusive, completion)
        };
    }

    public static bool TryParseCompletion(string? value, out SdsExportCompletionFilter filter)
    {
        filter = SdsExportCompletionFilter.All;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "all":
                return true;
            case "complete":
            case "completed":
                filter = SdsExportCompletionFilter.Complete;
                return true;
            case "incomplete":
            case "draft":
                filter = SdsExportCompletionFilter.Incomplete;
                return true;
            default:
                return false;
        }
    }

    public static DateTime? ToExclusiveEnd(DateTime? to)
    {
        if (!to.HasValue)
            return null;

        var value = to.Value;
        if (value.TimeOfDay == TimeSpan.Zero)
            return value.Date.AddDays(1);

        return value;
    }

    private static void WriteBanner(
        IXLWorksheet ws,
        DateTime? from,
        DateTime? toInclusive,
        SdsExportCompletionFilter completion,
        IReadOnlyList<SdsExportRow> rows)
    {
        var title = ws.Range(1, 1, 1, ColCount);
        title.Merge();
        title.Value = "FRANC  ·  SDS Assessment Report";
        title.Style.Font.FontName = "Calibri";
        title.Style.Font.FontSize = 18;
        title.Style.Font.Bold = true;
        title.Style.Font.FontColor = TitleWhite;
        title.Style.Fill.BackgroundColor = Navy;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        title.Style.Alignment.Indent = 1;
        ws.Row(1).Height = 32;

        var subtitle = ws.Range(2, 1, 2, ColCount);
        subtitle.Merge();
        subtitle.Value = BuildSubtitle(from, toInclusive, completion);
        subtitle.Style.Font.FontSize = 11;
        subtitle.Style.Font.FontColor = TitleWhite;
        subtitle.Style.Fill.BackgroundColor = NavySoft;
        subtitle.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        subtitle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        subtitle.Style.Alignment.Indent = 1;
        ws.Row(2).Height = 20;

        var completed = rows.Count(r => r.IsCompleted);
        var incomplete = rows.Count - completed;
        var uniqueUsers = rows.Select(r => r.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var rate = rows.Count == 0 ? 0 : Math.Round(100.0 * completed / rows.Count, 1);

        WriteKpi(ws, 4, 1, 2, "Total attempts", rows.Count.ToString("N0"));
        WriteKpi(ws, 4, 3, 1, "Completed", completed.ToString("N0"));
        WriteKpi(ws, 4, 4, 2, "Incomplete", incomplete.ToString("N0"));
        WriteKpi(ws, 4, 6, 2, "Unique users", uniqueUsers.ToString("N0"));
        WriteKpi(ws, 4, 8, 1, "Completion rate", $"{rate:0.0}%");

        ws.Row(4).Height = 16;
        ws.Row(5).Height = 22;
        ws.Row(6).Height = 8;
        ws.Row(7).Height = 8;
    }

    private static void WriteKpi(
        IXLWorksheet ws,
        int row,
        int startCol,
        int span,
        string label,
        string value)
    {
        var endCol = startCol + span - 1;
        var labelRange = ws.Range(row, startCol, row, endCol);
        var valueRange = ws.Range(row + 1, startCol, row + 1, endCol);
        if (span > 1)
        {
            labelRange.Merge();
            valueRange.Merge();
        }

        labelRange.Value = label.ToUpperInvariant();
        labelRange.Style.Font.FontSize = 8;
        labelRange.Style.Font.Bold = true;
        labelRange.Style.Font.FontColor = Muted;
        labelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        labelRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        labelRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F9FC");

        valueRange.Value = value;
        valueRange.Style.Font.FontSize = 16;
        valueRange.Style.Font.Bold = true;
        valueRange.Style.Font.FontColor = Navy;
        valueRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        valueRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F9FC");

        var box = ws.Range(row, startCol, row + 1, endCol);
        box.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        box.Style.Border.OutsideBorderColor = KpiBorder;
    }

    private static void WriteTable(IXLWorksheet ws, int headerRow, IReadOnlyList<SdsExportRow> rows)
    {
        string[] headers =
        [
            "#",
            "Full name",
            "Email",
            "Holland code",
            "Status",
            "Attempt",
            "Started (UTC)",
            "Completed (UTC)"
        ];

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = TitleWhite;
            cell.Style.Font.FontSize = 10;
            cell.Style.Fill.BackgroundColor = HeaderFill;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.BottomBorderColor = Navy;
        }

        ws.Row(headerRow).Height = 22;

        if (rows.Count == 0)
        {
            var empty = ws.Range(headerRow + 1, 1, headerRow + 1, ColCount);
            empty.Merge();
            empty.Value = "No SDS attempts match the selected filters.";
            empty.Style.Font.Italic = true;
            empty.Style.Font.FontColor = Muted;
            empty.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            empty.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            empty.Style.Fill.BackgroundColor = AltRow;
            ws.Row(headerRow + 1).Height = 24;
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = headerRow + 1 + i;
            var fill = i % 2 == 1 ? AltRow : XLColor.White;
            var status = row.IsCompleted ? "Completed" : "Incomplete";
            var name = $"{row.FirstName} {row.LastName}".Trim();
            var holland = string.IsNullOrWhiteSpace(row.HollandCode)
                ? Dash
                : row.HollandCode.Trim().ToUpperInvariant();

            ws.Cell(excelRow, 1).Value = i + 1;
            ws.Cell(excelRow, 2).Value = name;
            ws.Cell(excelRow, 3).Value = row.Email;
            ws.Cell(excelRow, 4).Value = holland;
            ws.Cell(excelRow, 5).Value = status;
            ws.Cell(excelRow, 6).Value = row.AttemptNumber;
            WriteDateCell(ws.Cell(excelRow, 7), row.StartedAt);
            WriteDateCell(ws.Cell(excelRow, 8), row.CompletedAt);

            var range = ws.Range(excelRow, 1, excelRow, ColCount);
            range.Style.Fill.BackgroundColor = fill;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            range.Style.Border.BottomBorderColor = Line;
            ws.Row(excelRow).Height = 20;

            ws.Cell(excelRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(excelRow, 1).Style.Font.FontColor = Muted;
            ws.Cell(excelRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(excelRow, 4).Style.Font.Bold = true;
            ws.Cell(excelRow, 4).Style.Font.FontName = "Consolas";
            if (holland != Dash)
                ws.Cell(excelRow, 4).Style.Fill.BackgroundColor = HollandFill;
            ws.Cell(excelRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(excelRow, 5).Style.Font.Bold = true;
            if (row.IsCompleted)
            {
                ws.Cell(excelRow, 5).Style.Font.FontColor = CompleteText;
                ws.Cell(excelRow, 5).Style.Fill.BackgroundColor = CompleteFill;
            }
            else
            {
                ws.Cell(excelRow, 5).Style.Font.FontColor = IncompleteText;
                ws.Cell(excelRow, 5).Style.Fill.BackgroundColor = IncompleteFill;
            }

            ws.Cell(excelRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(excelRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(excelRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var lastRow = headerRow + rows.Count;
        ws.Range(headerRow, 1, lastRow, ColCount).SetAutoFilter();
        ws.SheetView.FreezeRows(headerRow);

        var noteRow = lastRow + 2;
        var note = ws.Range(noteRow, 1, noteRow, ColCount);
        note.Merge();
        note.Value = "Holland code is shown for completed assessments only. AI results are not included in this export.";
        note.Style.Font.FontSize = 9;
        note.Style.Font.Italic = true;
        note.Style.Font.FontColor = Muted;
    }

    private static void WriteDateCell(IXLCell cell, DateTime? value)
    {
        if (!value.HasValue)
        {
            cell.Value = Dash;
            cell.Style.Font.FontColor = Muted;
            return;
        }

        cell.Value = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
        cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
    }

    private static void ApplyPrintLayout(IXLWorksheet ws, int headerRow, int lastDataRow)
    {
        ws.Column(1).Width = 6;
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 36;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 16;
        ws.Column(6).Width = 12;
        ws.Column(7).Width = 22;
        ws.Column(8).Width = 22;

        ws.SheetView.FreezeRows(headerRow);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.Margins.Top = 0.5;
        ws.PageSetup.Margins.Bottom = 0.5;
        ws.PageSetup.Margins.Left = 0.4;
        ws.PageSetup.Margins.Right = 0.4;
        ws.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
        ws.PageSetup.Header.Left.AddText("Franc — SDS Assessment Report");
        ws.PageSetup.Footer.Left.AddText("Confidential");
        ws.PageSetup.Footer.Right.AddText("&P / &N");
        ws.PageSetup.PrintAreas.Add(1, 1, lastDataRow + 2, ColCount);
    }

    private static string BuildSubtitle(
        DateTime? from,
        DateTime? toInclusive,
        SdsExportCompletionFilter completion)
    {
        var period = from.HasValue || toInclusive.HasValue
            ? $"{FormatFilterDate(from) ?? "Start"}  →  {FormatFilterDate(toInclusive) ?? "Present"}"
            : "All dates";

        var generated = DateTime.UtcNow.ToString("dd MMM yyyy, HH:mm") + " UTC";
        return $"Period: {period}     ·     Status: {CompletionLabel(completion)}     ·     Generated: {generated}";
    }

    private static string BuildFileName(
        DateTime? from,
        DateTime? toInclusive,
        SdsExportCompletionFilter completion)
    {
        var fromPart = from?.ToString("yyyyMMdd") ?? "start";
        var toPart = toInclusive?.ToString("yyyyMMdd") ?? "end";
        var status = completion.ToString().ToLowerInvariant();
        return $"Franc_SDS_Report_{status}_{fromPart}_{toPart}.xlsx";
    }

    private static string CompletionLabel(SdsExportCompletionFilter completion) =>
        completion switch
        {
            SdsExportCompletionFilter.Complete => "Completed only",
            SdsExportCompletionFilter.Incomplete => "Incomplete only",
            _ => "All attempts"
        };

    private static string? FormatFilterDate(DateTime? value) =>
        value?.ToString(value.Value.TimeOfDay == TimeSpan.Zero ? "dd MMM yyyy" : "dd MMM yyyy HH:mm");
}
