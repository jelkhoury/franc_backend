namespace FrancProject.Repositories
{
    public static class GameQuizConstants
    {
        public const int QuestionsPerSession = 10;

        public const string StatusInProgress = "InProgress";
        public const string StatusCompleted = "Completed";
        public const string StatusFailed = "Failed";

        public const int MaxSkipPerSession = 1;
        public const int MaxFiftyFiftyPerSession = 2;
        public const int MaxDoubleChancePerSession = 1;
        public const int MaxTimeFreezePerSession = 1;
        public const int MaxHintPerSession = 1;

        public static int PointsForLevelPass(int levelNumber) => 100 * levelNumber;

        public static bool IsValidOptionLetter(string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 1)
                return false;
            var c = char.ToUpperInvariant(value[0]);
            return c is 'A' or 'B' or 'C' or 'D';
        }
    }
}
