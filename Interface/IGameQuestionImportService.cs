using FrancProject.Dto;

namespace FrancProject.Interfaces
{
    public interface IGameQuestionImportService
    {
        /// <summary>
        /// Imports quiz questions from an .xlsx workbook stream.
        /// Replaces active questions per level when possible; falls back to deactivating old rows if FKs block delete.
        /// </summary>
        Task<GameQuestionImportResultDto> ImportFromExcelAsync(Stream excelStream, CancellationToken cancellationToken = default);
    }
}
