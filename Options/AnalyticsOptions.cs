namespace FrancProject.Options
{
    public class AnalyticsOptions
    {
        public const string SectionName = "Analytics";

        /// <summary>
        /// When true, admin analytics reads from ActivityEvents first (falls back to legacy union if empty).
        /// </summary>
        public bool UseActivityEvents { get; set; } = false;

        /// <summary>
        /// Cache admin analytics JSON responses in memory. Safe to disable anytime.
        /// </summary>
        public bool EnableResponseCache { get; set; } = true;

        /// <summary>
        /// How long each cached analytics response lives before it is refreshed. Default 1 day.
        /// </summary>
        public double CacheDurationDays { get; set; } = 1;
    }
}
