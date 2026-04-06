namespace FrancProject.Models
{
    public class GameSession
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public long LevelId { get; set; }
        public GameLevel Level { get; set; } = null!;

        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }

        public int Score { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public bool Passed { get; set; }

        /// <summary>InProgress, Completed, or Failed.</summary>
        public string Status { get; set; } = null!;

        public ICollection<GameSessionAnswer> Answers { get; set; } = new List<GameSessionAnswer>();
    }
}
