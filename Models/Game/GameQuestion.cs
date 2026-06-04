namespace FrancProject.Models
{
    public class GameQuestion
    {
        public long Id { get; set; }
        public long LevelId { get; set; }
        public GameLevel Level { get; set; } = null!;

        public string QuestionText { get; set; } = null!;
        public string OptionA { get; set; } = null!;
        public string OptionB { get; set; } = null!;
        public string OptionC { get; set; } = null!;
        public string OptionD { get; set; } = null!;
        /// <summary>Single letter A, B, C, or D.</summary>
        public string CorrectOption { get; set; } = null!;
        public string? Hint { get; set; }
        public bool IsActive { get; set; }

        public ICollection<GameSessionAnswer> SessionAnswers { get; set; } = new List<GameSessionAnswer>();
    }
}
