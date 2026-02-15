using ClosedXML.Excel;
using FrancProject.Data;
using Microsoft.EntityFrameworkCore;

public class JobComparisonExcelService
{
    private readonly DataContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly BlobStorageService _blobStorage;

    public JobComparisonExcelService(
           DataContext context,
           IWebHostEnvironment env,
           BlobStorageService blobService)
    {
        _context = context;
        _env = env;
        _blobStorage = blobService;
    }

    public async Task<ExcelExportResultDto> GenerateJobComparisonExcelAsync(int userId, int jobComparisonId)
    {
        var comparison = await _context.JobComparisons
            .Include(j => j.Answers)
            .FirstOrDefaultAsync(j =>
                j.Id == jobComparisonId &&
                j.UserId == userId);

        if (comparison == null)
            throw new Exception("Job comparison not found");

        var criteria = await _context.JobComparisonCriteria
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        var answersLookup = comparison.Answers
            .ToDictionary(a => a.CriterionId);

        var templatePath = Path.Combine(
            _env.ContentRootPath,
            "Templates",
            "Job Comparison Scorecard.xlsx");

        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheet(1);
        var sectionRows = new HashSet<int> { 9, 15, 22, 35, 39 };

        int row = 6;

        foreach (var criterion in criteria)
        {
            while (sectionRows.Contains(row))
                row++;

            answersLookup.TryGetValue(criterion.Id, out var a);

            if (a != null)
            {
                sheet.Cell(row, "B").Value = a.Weight;

                sheet.Cell(row, "C").Value =
                    a.NotApplicableA ? 0 : a.ScoreA;

                sheet.Cell(row, "E").Value =
                    a.NotApplicableB ? 0 : a.ScoreB;
            }
            else
            {
                sheet.Cell(row, "B").Value = 0;
                sheet.Cell(row, "C").Value = 0;
                sheet.Cell(row, "E").Value = 0;
            }

            row++;
        }

        byte[] excelBytes;

        using (var ms = new MemoryStream())
        {
            workbook.SaveAs(ms);
            excelBytes = ms.ToArray();
        }

        var excelUrl =
            await _blobStorage.UploadJobComparisonExcelAsync(jobComparisonId, excelBytes);

        comparison.ExcelResultUrl = excelUrl;
        await _context.SaveChangesAsync();

        return new ExcelExportResultDto
        {
            Bytes = excelBytes,
            ExcelUrl = excelUrl
        };
    }


    public class ExcelExportResultDto
    {
        public byte[] Bytes { get; set; }
        public string ExcelUrl { get; set; }
    }

}
