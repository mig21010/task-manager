using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TaskManager.Api.Data;
using TaskManager.Api.Models;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClaudeAgentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly RagService _ragService;

        public ClaudeAgentController(
            AppDbContext context,
            IConfiguration config,
            RagService ragService)
        {
            _context = context;
            _config = config;
            _http = new HttpClient();
            _ragService = ragService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat(
            [FromBody] ClaudeChatRequest request)
        {

            Conversation conversation;

            if(request.ConversationId.HasValue) 
            {
                conversation = await _context.Conversations.Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId)
                    ?? new Conversation
                    {
                        Title = request.Message[..Math.Min(50, request.Message.Length)],
                        CreatedAt = DateTime.UtcNow,
                     
                    };

            } 
            else 
            {
                conversation = new Conversation
                {
                    Title = request.Message[..Math.Min(50, request.Message.Length)],
                    CreatedAt = DateTime.UtcNow,
                 
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            var userMessage = new ConversationMessage
            {
                Role = "user",
                Content = request.Message,
                CreatedAt = DateTime.UtcNow,
                ConversationId = conversation.Id
            };

            _context.ConversationMessages.Add(userMessage);
            await _context.SaveChangesAsync();

            var history = conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList<object>();

            var assistantMessage = new ConversationMessage
            {
                Role = "assistant",
                Content = "",
                CreatedAt = DateTime.UtcNow,
                ConversationId = conversation.Id
            };

            _context.ConversationMessages.Add(assistantMessage);
            await _context.SaveChangesAsync();

            var apiKey = _config["Anthropic:ApiKey"];

            var body = new
            {
                model = "claude-haiku-4-5",
                max_tokens = 1024,
                system = "You are a task management assistant. " +
                 "Help users manage their tasks using the available tools. " +
                 "Respond in the same language the user writes in. " +
                 "IMPORTANT: When a user asks to complete a task by name, " +
                 "first use search_similar_tasks to find it, " +
                 "then immediately use complete_task with the found ID " +
                 "without asking for confirmation.",
                messages = history.ToList(),
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
                    },
                    new
                    {
                        name = "search_similar_tasks",
                        description = "Searches for tasks semantically similar to a query using RAG",
                        input_schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new {
                                    type = "string",
                                    description = "The search query to find similar tasks"
                                }
                            },
                            required = new[] { "query" }
                        }
                    },
                    new
                    {
                        name = "complete_task",
                        description = "Marks a specific task as completed by its ID or title",
                        input_schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                taskId = new {
                                    type = "integer",
                                    description = "The ID of the task to complete"
                                }
                            },
                            required = new[] { "taskId" }
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
                var toolId = toolBlock
                    .GetProperty("id").GetString()!;

                var result = await ExecuteFunction(toolName, toolInput);

                // Si fue search → pasar resultado a Claude
                // para que decida el siguiente paso
                if (toolName == "search_similar_tasks")
                {
                    // Agregar al historial:
                    // 1. Respuesta de Claude con tool_use
                    // 2. Resultado de la tool
                    var assistantContent = doc.RootElement
                        .GetProperty("content").ToString();

                    var toolResultMessages = new List<object>(history)
        {
            new {
                role = "assistant",
                content = System.Text.Json.JsonSerializer
                    .Deserialize<object>(assistantContent)
            },
            new {
                role = "user",
                content = new object[]
                {
                    new {
                        type = "tool_result",
                        tool_use_id = toolId,
                        content = result
                    }
                }
            }
        };

                    // Segunda llamada a Claude con el resultado
                    var body2 = new
                    {
                        model = "claude-haiku-4-5",
                        max_tokens = 1024,
                        system = body.system,
                        messages = toolResultMessages,
                        tools = body.tools
                    };

                    var json2 = JsonSerializer.Serialize(body2);
                    var content2 = new StringContent(
                        json2, Encoding.UTF8, "application/json");

                    _http.DefaultRequestHeaders.Clear();
                    _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    _http.DefaultRequestHeaders.Add(
                        "anthropic-version", "2023-06-01");

                    var response2 = await _http.PostAsync(
                        "https://api.anthropic.com/v1/messages",
                        content2);

                    var responseStr2 = await response2.Content
                        .ReadAsStringAsync();
                    var doc2 = JsonDocument.Parse(responseStr2);

                    // Si Claude quiere usar otra tool
                    if (doc2.RootElement.GetProperty("stop_reason")
                        .GetString() == "tool_use")
                    {
                        var toolBlock2 = doc2.RootElement
                            .GetProperty("content")
                            .EnumerateArray()
                            .First(c => c.GetProperty("type")
                                .GetString() == "tool_use");

                        var toolName2 = toolBlock2
                            .GetProperty("name").GetString()!;
                        var toolInput2 = toolBlock2
                            .GetProperty("input").ToString();

                        var result2 = await ExecuteFunction(
                            toolName2, toolInput2);

                        return Ok(new
                        {
                            reply = result2,
                            conversationId = conversation.Id
                        });
                    }

                    // Claude respondió con texto
                    var text2 = doc2.RootElement
                        .GetProperty("content")
                        .EnumerateArray()
                        .First(c => c.GetProperty("type")
                            .GetString() == "text")
                        .GetProperty("text").GetString();

                    return Ok(new
                    {
                        reply = text2 ?? result,
                        conversationId = conversation.Id
                    });
                }

                return Ok(new
                {
                    reply = result,
                    conversationId = conversation.Id
                });
            }

            var text = doc.RootElement
                .GetProperty("content")
                .EnumerateArray()
                .First(c => c.GetProperty("type")
                    .GetString() == "text")
                .GetProperty("text").GetString();

            var reply = text ?? "";

            assistantMessage.Content = reply;
            _context.ConversationMessages
                .Update(assistantMessage);
            await _context.SaveChangesAsync();

            return Ok(new { reply, conversationId = conversation.Id });
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

                //index pinecone
                await _ragService.UpsertTaskAsync(
                    task.Id,
                    task.Title,
                    task.Description,
                    false);


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

            if (name == "search_similar_tasks")
            {
                var args = JsonDocument.Parse(argsJson);
                var query = args.RootElement
                    .GetProperty("query").GetString()!;

                var results = await _ragService
                    .SearchSimilarAsync(query);

                if (!results.Any())
                    return "No similar tasks found.";

                var list = string.Join("\n", results
                    .Select(r => $"- [{r.Id}] {r.Title}"));

                return $"Found similar tasks:\n{list}";
            }


            if (name == "complete_task")
            {
                var args = JsonDocument.Parse(argsJson);
                var taskId = args.RootElement
                    .GetProperty("taskId").GetInt32();

                var task = await _context.Tasks
                    .FindAsync(taskId);

                if (task == null)
                    return $"❌ Task {taskId} not found";

                task.IsCompleted = true;
                await _context.SaveChangesAsync();

                // Actualizar en Pinecone
                await _ragService.UpsertTaskAsync(
                    task.Id,
                    task.Title,
                    task.Description,
                    true);

                return $"✅ Task \"{task.Title}\" completed!";
            }

            return "Unknown function";
        }
    }

    public class ClaudeChatRequest
    {
        public string Message { get; set; } = "";
        public int? ConversationId { get; set; }
    }
}