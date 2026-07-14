using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using TaskManager.Api.Data;
using TaskManager.Api.Models;
using System.Text.Json;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class AgentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;


        public AgentController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var apiKey = _config["OpenAI:ApiKey"];
            var client = new ChatClient(
                model: "gpt-4o-mini",
                apiKey: apiKey);

            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    "create_task",
                    "Creates a new Task",
                    BinaryData.FromString("""
                        {   "type": "object",
                            "properties": {
                                "title": {
                                    "type": "string",
                                    "description": "The title of the task"
                                },
                                "description": {
                                    "type": "string",
                                    "description": "The description of the task"
                                }
                            },
                            "required": ["title"]
                        }
                        """)),

                ChatTool.CreateFunctionTool(
                    "get_summary",
                    "Gets summary of all tasks",
                    BinaryData.FromString("""
                        {   "type": "object",
                            "properties": {}
                          
                        }
                        """)),

                ChatTool.CreateFunctionTool(
                    "complete_all_task",
                    "Marks all pending tasks as completed",
                    BinaryData.FromString("""
                        {   "type": "object",
                            "properties": {}
                          
                        }
                        """))

            };

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "You are a task management assistant. " +
                    "Help users manage their tasks using " +
                    "the available functions. " +
                    "Always respond in the same language " +
                    "the user writes in."),
                new UserChatMessage(request.Message)
            };

            var response = await client.CompleteChatAsync(
                messages, new ChatCompletionOptions
                {
                    Tools = { tools[0], tools[1], tools[2] },
                });

            //si el modelo quiere llamar a una funcion

            if (response.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                var toolCall = response.Value.ToolCalls[0];
                var result = await ExecuteFunction(
                    toolCall.FunctionName, toolCall.FunctionArguments.ToString());

                return Ok(new { reply = result });

            }

            return Ok(new { reply = response.Value.Content[0].Text });
        }

        private async Task<string> ExecuteFunction(string name, string argsJson)
        {
            if (name == "create_task")
            {
                var args = JsonDocument.Parse(argsJson);
                var title = args.RootElement.GetProperty("title").GetString();
                var description = args.RootElement.TryGetProperty("description", out var descProp)
                    ? descProp.GetString()
                    : "";

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
                var pending = await _context.Tasks.CountAsync(t => !t.IsCompleted);
                var completed = await _context.Tasks.CountAsync(t => t.IsCompleted);

                return $"📊 Task Summary:\n" +
                       $"- Total tasks: {total}\n" +
                       $"- Pending tasks: {pending}\n" +
                       $"- Completed tasks: {completed}";
            }

            if (name == "complete_all_task")
            {
                var tasks = await _context.Tasks
                    .Where(t => !t.IsCompleted)
                    .ToListAsync();
                foreach (var task in tasks)
                {
                    task.IsCompleted = true;
                }
                await _context.SaveChangesAsync();
                return $"✅ {tasks.Count} tasks completed!";
            }

            return "❌ Unknown function.";
        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
        }
    }
}
