namespace FrancProject.Models
{
    public class ChatMessage
    {
        public Guid Id { get; set; }          // uniqueidentifier → Guid

        public int UserId { get; set; } 
        public string SessionId { get; set; } = null!; // nvarchar → string

        public string Role { get; set; } = null!;
        public string Content { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public string? MetaJson { get; set; }
        public bool IsAdminOnly { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
