namespace FrancProject.Models
{
    public class GameSessionAnswer
    {
        public long Id { get; set; }
        public long SessionId { get; set; }
        public GameSession Session { get; set; } = null!;
        public long QuestionId { get; set; }
        public GameQuestion Question { get; set; } = null!;

        /// <summary>Order within the session (0..9 for 10 questions).</summary>
        public int QuestionOrder { get; set; }

        /// <summary>Submissions for this slot; at most 2 when UsedDoubleChance.</summary>
        public int AnswerAttemptCount { get; set; }

        public string? SelectedOption { get; set; }
        public bool IsCorrect { get; set; }
        public bool UsedSkip { get; set; }
        public bool UsedFiftyFifty { get; set; }
        public bool UsedDoubleChance { get; set; }
        public bool UsedTimeFreeze { get; set; }
        public bool UsedHint { get; set; }
        public DateTimeOffset? AnsweredAt { get; set; }
    }
}
