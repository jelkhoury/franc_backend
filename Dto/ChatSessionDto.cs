namespace FrancProject.Dto
{
    public class ChatSessionDto
    {
        public string SessionId { get; set; }
        public string Email { get; set; } = null!;      // One email per session
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

}
