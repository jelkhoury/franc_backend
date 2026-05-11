namespace FrancProject.DTOs
{
    public class StartGameSessionRequestDto
    {
        public int LevelNumber { get; set; }
    }

    public class SubmitGameAnswerRequestDto
    {
        public string? SelectedOption { get; set; } = null!;
        public bool TimedOut { get; set; }
    }

    public class UseGameAbilityRequestDto
    {
        /// <summary>Skip, FiftyFifty, DoubleChance, TimeFreeze, Hint</summary>
        public string Ability { get; set; } = null!;
    }

    public class GameAbilitiesRemainingDto
    {
        public int Skip { get; set; }
        public int FiftyFifty { get; set; }
        public int DoubleChance { get; set; }
        public int TimeFreeze { get; set; }
        public int Hint { get; set; }
    }

    public class GameQuestionClientDto
    {
        public long SessionAnswerId { get; set; }
        public int QuestionOrder { get; set; }
        public string QuestionText { get; set; } = null!;
        public string OptionA { get; set; } = null!;
        public string OptionB { get; set; } = null!;
        public string OptionC { get; set; } = null!;
        public string OptionD { get; set; } = null!;
        /// <summary>Option letters hidden by FiftyFifty (e.g. C, D).</summary>
        public List<string> HiddenOptions { get; set; } = new();
        public string? Hint { get; set; }
        public string? SelectedOption { get; set; }
        public bool IsResolved { get; set; }
        public bool IsCorrect { get; set; }
        public bool UsedSkip { get; set; }
        public bool UsedFiftyFifty { get; set; }
        public bool UsedDoubleChance { get; set; }
        public bool UsedTimeFreeze { get; set; }
        public bool UsedHint { get; set; }
        /// <summary>True when first attempt was wrong but a second try is allowed.</summary>
        public bool AwaitingDoubleChanceRetry { get; set; }
    }

    public class GameSessionStateDto
    {
        public long SessionId { get; set; }
        public int LevelNumber { get; set; }
        public string LevelName { get; set; } = null!;
        public string BadgeName { get; set; } = null!;
        public int PassScore { get; set; }
        public string Status { get; set; } = null!;
        public int Score { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int ResolvedCount { get; set; }
        public int TotalQuestions { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public bool Passed { get; set; }
        public GameAbilitiesRemainingDto AbilitiesRemaining { get; set; } = new();
        public List<GameQuestionClientDto> Questions { get; set; } = new();
    }

    public class UserGameProgressDto
    {
        public long ?ActiveSessionId { get; set; }
        public int CurrentLevel { get; set; }
        public int HighestUnlockedLevel { get; set; }
        public bool BronzeBadgeEarned { get; set; }
        public bool SilverBadgeEarned { get; set; }
        public bool GoldBadgeEarned { get; set; }
        public bool PlatinumBadgeEarned { get; set; }
        public bool DiamondBadgeEarned { get; set; }
        public int TotalPoints { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public Dictionary<int, int>? LevelScores { get; set; }
    }

    /// <summary>All hint texts for questions in one quiz session (same order as session questions).</summary>
    public class GameSessionHintsDto
    {
        public long SessionId { get; set; }
        public List<GameQuestionHintItemDto> Hints { get; set; } = new();
    }

    public class GameQuestionHintItemDto
    {
        public long SessionAnswerId { get; set; }
        public int QuestionOrder { get; set; }
        public long QuestionId { get; set; }
        public string? Hint { get; set; }
    }
}
