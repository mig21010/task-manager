namespace TaskManager.Api.Models
{
    public class ConversationMessage
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = ""; 
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Conversation Conversation { get; set; } = null!;
    }
}
