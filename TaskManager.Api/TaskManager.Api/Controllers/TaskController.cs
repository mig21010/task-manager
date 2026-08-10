using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Models;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RagService _ragService;
        public TasksController(AppDbContext context, RagService ragService) {
            _context = context;
            _ragService = ragService;
        }

        //GET: api/tasks
        [HttpGet]
        public async Task<IActionResult>GetAll()
        {             
            var tasks = await _context.Tasks.ToListAsync();
            return Ok(tasks);
        }

        //GET: api/tasks/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }
            return Ok(task);
        }

        //POST: api/tasks
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaskItem task)
        {
            task.CreatedAt = DateTime.UtcNow;
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            try
            {

                //saved task to database, now we can call the RAG service to get the RAG status for this task
                await _ragService.UpsertTaskAsync(
                task.Id,
                task.Title,
                task.Description);
            }
            catch (Exception ex)
            {
                // Log el error pero no falla
                // la creación de la tarea
                Console.WriteLine(
                    $"RAG Error: {ex.Message}");
                Console.WriteLine($"Inner: {ex.InnerException?.Message}");
            }

            return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
        }

        //PUT: api/tasks/{id}

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] TaskItem task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }
            _context.Entry(task).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return NoContent();
        }

        //DELETE: api/tasks/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool TaskExists(int id)
        {
            throw new NotImplementedException();
        }

        //GET: api/tasks/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var total = await _context.Tasks.CountAsync();
            var pending = await _context.Tasks.CountAsync(t => !t.IsCompleted);
            var completed = await _context.Tasks.CountAsync(t => t.IsCompleted);
            var summary = new
            {
                TotalTasks = total,
                Pending = pending,
                CompletedTask = completed
            };
            return Ok(summary);
        }
    }
}
