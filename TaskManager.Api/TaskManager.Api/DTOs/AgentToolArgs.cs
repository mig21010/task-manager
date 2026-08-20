namespace TaskManager.Api.DTOs
{
    public class CreateTaskArgs
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
    }

    public class CompleteTaskArgs
    {
        public int TaskId { get; set; }
    }

    public class SearchTaskArgs
    {
        public string Query { get; set; } = "";
    }
}
