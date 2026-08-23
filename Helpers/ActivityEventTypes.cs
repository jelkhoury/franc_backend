namespace FrancProject.Helpers
{
    public static class ActivityEventTypes
    {
        public const string SdsStarted = "sds.started";
        public const string SdsCompleted = "sds.completed";
        public const string MockInterviewSubmitted = "mockInterview.submitted";
        public const string MockInterviewEvaluated = "mockInterview.evaluated";
        public const string MockInterviewReportGenerated = "mockInterview.report_generated";
        public const string JobComparisonStarted = "jobComparison.started";
        public const string JobComparisonCompleted = "jobComparison.completed";
        public const string GamificationLevelCompleted = "gamification.level_completed";
        public const string GamificationLevelFailed = "gamification.level_failed";
        public const string ResumeUploaded = "resume.uploaded";
        public const string CoverLetterUploaded = "coverLetter.uploaded";
    }
}
