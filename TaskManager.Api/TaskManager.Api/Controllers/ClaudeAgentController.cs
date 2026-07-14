using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClaudeAgentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public ClaudeAgentController(
            AppDbContext context,
            IConfiguration config)
        {
            _context = context;
            _config = config;
            _http = new HttpClient();
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat(
            [FromBody] ClaudeChatRequest request)
        {
            var apiKey = _config["Anthropic:ApiKey"];

            var body = new
            {
                model = "claude-haiku-4-5",
                max_tokens = 1024,
                system = "You are a task management assistant. Help users manage their tasks using the available tools. Respond in the same language the user writes in.",
                messages = new[]
                {
                    new { role = "user", content = request.Message }
                },
                tools = new object[]
                {
                    new
                    {
                        name = "create_task",
                        description = "Creates a new task",
                        input_schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                title = new { type = "string", description = "Task title" },
                                description = new { type = "string", description = "Task description" }
                            },
                            required = new[] { "title" }
                        }
                    },
                    new
                    {
                        name = "get_summary",
                        description = "Gets summary of all tasks",
                        input_schema = new
                        {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    },
                    new
                    {
                        name = "complete_all_tasks",
                        description = "Marks all pending tasks as completed",
                        input_schema = new
                        {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(
                json, Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders
                .Add("x-api-key", apiKey);
            _http.DefaultRequestHeaders
                .Add("anthropic-version", "2023-06-01");

            var response = await _http.PostAsync(
                "https://api.anthropic.com/v1/messages",
                content);

            var responseStr = await response.Content.ReadAsStringAsync();

            Console.WriteLine(responseStr);

            var doc = JsonDocument.Parse(responseStr);
            var stopReason = doc.RootElement
                .GetProperty("stop_reason").GetString();

            if (stopReason == "tool_use")
            {
                var toolBlock = doc.RootElement
                    .GetProperty("content")
                    .EnumerateArray()
                    .First(c => c.GetProperty("type")
                        .GetString() == "tool_use");

                var toolName = toolBlock
                    .GetProperty("name").GetString()!;
                var toolInput = toolBlock
                    .GetProperty("input").ToString();

                var result = await ExecuteFunction(
                    toolName, toolInput);

                return Ok(new { reply = result });
            }

            var text = doc.RootElement
                .GetProperty("content")
                .EnumerateArray()
                .First(c => c.GetProperty("type")
                    .GetString() == "text")
                .GetProperty("text").GetString();

            return Ok(new { reply = text });
        }

        private async Task<string> ExecuteFunction(
            string name, string argsJson)
        {
            if (name == "create_task")
            {
                var args = JsonDocument.Parse(argsJson);
                var title = args.RootElement
                    .GetProperty("title").GetString();
                var description = args.RootElement
                    .TryGetProperty("description", out var desc)
                    ? desc.GetString() : "";

                var task = new TaskItem
                {
                    Title = title!,
                    Description = description,
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();
                return $"✅ Task \"{title}\" created!";
            }

            if (name == "get_summary")
            {
                var total = await _context.Tasks.CountAsync();
                var pending = await _context.Tasks
                    .CountAsync(t => !t.IsCompleted);
                var completed = await _context.Tasks
                    .CountAsync(t => t.IsCompleted);
                return $"📊 Total: {total} | Pending: {pending} | Completed: {completed}";
            }

            if (name == "complete_all_tasks")
            {
                var tasks = await _context.Tasks
                    .Where(t => !t.IsCompleted)
                    .ToListAsync();
                foreach (var task in tasks)
                    task.IsCompleted = true;
                await _context.SaveChangesAsync();
                return $"✅ {tasks.Count} tasks completed!";
            }

            return "Unknown function";
        }
    }

    public class ClaudeChatRequest
    {
        public string Message { get; set; } = "";
    }
}