namespace FrancProject.Dto
{
    public class ChatMessageDto
    {
        public string Role { get; set; } = null!;       // "user" | "assistant"
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
