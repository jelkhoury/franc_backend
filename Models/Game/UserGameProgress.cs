namespace FrancProject.Models
{
    public class UserGameProgress
    {
        public long Id { get; set; }
        public int UserId { get; set; }

        public int CurrentLevel { get; set; }
        public int HighestUnlockedLevel { get; set; }

        public bool BronzeBadgeEarned { get; set; }
        public bool SilverBadgeEarned { get; set; }
        public bool GoldBadgeEarned { get; set; }
        public bool PlatinumBadgeEarned { get; set; }
        public bool DiamondBadgeEarned { get; set; }

        public int TotalPoints { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
