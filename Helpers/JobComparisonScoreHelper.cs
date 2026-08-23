namespace FrancProject.Helpers
{
    public static class JobComparisonScoreHelper
    {
        public record AnswerRow(
            int ScoreA,
            int ScoreB,
            int Weight,
            bool NotApplicableA,
            bool NotApplicableB,
            string Category);

        public static (double ScoreA, double ScoreB) ComputeWeightedScores(IEnumerable<AnswerRow> answers)
        {
            double weightedA = 0;
            double weightedB = 0;
            double totalWeightA = 0;
            double totalWeightB = 0;

            foreach (var answer in answers)
            {
                if (!answer.NotApplicableA)
                {
                    weightedA += answer.ScoreA * answer.Weight;
                    totalWeightA += answer.Weight;
                }

                if (!answer.NotApplicableB)
                {
                    weightedB += answer.ScoreB * answer.Weight;
                    totalWeightB += answer.Weight;
                }
            }

            var scoreA = totalWeightA > 0 ? weightedA / totalWeightA : 0;
            var scoreB = totalWeightB > 0 ? weightedB / totalWeightB : 0;
            return (scoreA, scoreB);
        }

        public static string DetermineWinner(double scoreA, double scoreB)
        {
            if (Math.Abs(scoreA - scoreB) < 0.001)
                return "Tie";

            return scoreA > scoreB ? "A" : "B";
        }

        public static (double ScoreA, double ScoreB, string Winner) ComputeComparisonResult(IEnumerable<AnswerRow> answers)
        {
            var (scoreA, scoreB) = ComputeWeightedScores(answers);
            return (scoreA, scoreB, DetermineWinner(scoreA, scoreB));
        }

        public static string FormatWinnerLabel(string winner, string jobAName, string jobBName) => winner switch
        {
            "A" => $"{jobAName} wins",
            "B" => $"{jobBName} wins",
            _ => "Tie"
        };
    }
}
