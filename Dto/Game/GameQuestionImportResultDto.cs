namespace FrancProject.Dto
{
    public class GameQuestionImportResultDto
    {
        public int TotalImported { get; set; }
        /// <summary>
        /// Non–level worksheets plus grid rows at 7,11,15,… where column A is not a numeric question number.
        /// </summary>
        public int SkippedBlocks { get; set; }
        public int FailedBlocks { get; set; }
        /// <summary>Level number (1–5) to count of rows inserted for that level.</summary>
        public Dictionary<int, int> ImportedPerLevel { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
