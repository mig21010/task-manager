namespace TaskManager.Api.Models
{
    public class Conversation
    {
        public int Id { get; set; }
        public string Title { get; set; } = "New Chat";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ConversationMessage> Messages { get; set; } = new();

    }
}
