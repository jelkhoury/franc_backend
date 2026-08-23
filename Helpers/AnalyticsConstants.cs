namespace FrancProject.Helpers
{
    public static class AnalyticsConstants
    {
        public const string MockInterview = "mockInterview";
        public const string Sds = "sds";
        public const string JobComparison = "jobComparison";
        public const string Gamification = "gamification";
        public const string Resume = "resume";
        public const string CoverLetter = "coverLetter";
        public const string Chat = "chat";
        public const string JobMatching = "jobMatching";

        public static readonly IReadOnlyList<string> AllServiceKeys = new[]
        {
            MockInterview, Sds, JobComparison, Gamification, Resume, CoverLetter, Chat
        };

        public static string GetServiceName(string serviceKey) => serviceKey switch
        {
            MockInterview => "Mock Interview",
            Sds => "Personality Test (SDS)",
            JobComparison => "Job Comparison",
            Gamification => "Career Quest",
            Resume => "Resume Feedback",
            CoverLetter => "Cover Letter Feedback",
            Chat => "Franc Chatbot",
            JobMatching => "Job Matching",
            _ => serviceKey
        };

        public static bool IsValidServiceKey(string? serviceKey) =>
            !string.IsNullOrWhiteSpace(serviceKey) &&
            (AllServiceKeys.Contains(serviceKey) || serviceKey == JobMatching);
    }
}
