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

    public async Task<ExcelExportResultDto>
 GenerateJobComparisonExcelAsync(int userId, int jobComparisonId)
    {
        var comparison = await _context.JobComparisons
            .Include(j => j.Answers)
            .FirstOrDefaultAsync(j =>
                j.Id == jobComparisonId &&
                j.UserId == userId);

        if (comparison == null)
            throw new Exception("Job comparison not found");

        var templatePath = Path.Combine(
            _env.ContentRootPath,
            "Templates",
            "Job Comparison Scorecard.xlsx");

        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheet(1);

        // Rows that represent section titles (must be skipped)
        var sectionRows = new HashSet<int> { 9, 15, 22, 35, 39 };

        int row = 6;

        foreach (var a in comparison.Answers.OrderBy(x => x.CriterionId))
        {
            // 🔥 Skip section header rows
            while (sectionRows.Contains(row))
            {
                row++;
            }

            if (a.NotApplicable)
            {
                sheet.Cell(row, "B").Value = 0;
                sheet.Cell(row, "C").Value = 0;
                sheet.Cell(row, "E").Value = 0;
            }
            else
            {
                sheet.Cell(row, "B").Value = a.Weight;
                sheet.Cell(row, "C").Value = a.ScoreA;
                sheet.Cell(row, "E").Value = a.ScoreB;
            }

            row++;
        }

        byte[] excelBytes;
        using (var ms = new MemoryStream())
        {
            workbook.SaveAs(ms);
            excelBytes = ms.ToArray();
        }

        // 🔥 USE EXISTING FUNCTION
        var excelUrl =
            await _blobStorage.UploadJobComparisonExcelAsync(jobComparisonId, excelBytes);

        // 🔥 save URL in DB
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
