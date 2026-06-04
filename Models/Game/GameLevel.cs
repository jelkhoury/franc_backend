namespace FrancProject.Models
{
    public class GameLevel
    {
        public long Id { get; set; }
        public int LevelNumber { get; set; }
        public string Name { get; set; } = null!;
        public string BadgeName { get; set; } = null!;
        public int PassScore { get; set; }
        public bool IsActive { get; set; }

        public ICollection<GameQuestion> Questions { get; set; } = new List<GameQuestion>();
        public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
    }
}
