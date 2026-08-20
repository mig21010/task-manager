using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Diagnostics;
using TaskManager.Api.Data;
using TaskManager.Api.Services;
using System.Text.Json;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<RagService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddHttpClient("ClaudeClient")
    .AddResilienceHandler("claude-pipeline", builder =>
    {
        // Retry con exponential backoff:
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true, // variación aleatoria
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r =>
                    r.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                    r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        });

        // Timeout por intento:
        builder.AddTimeout(TimeSpan.FromSeconds(30));
    });

builder.Services.AddHttpClient("ClaudeClient")
    .AddResilienceHandler("claude-pipeline", pipeline =>
    {
        // 1. Retry:
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r =>
                    r.StatusCode ==
                        System.Net.HttpStatusCode.TooManyRequests ||
                    r.StatusCode ==
                        System.Net.HttpStatusCode.ServiceUnavailable)
        });

        // 2. Timeout:
        pipeline.AddTimeout(TimeSpan.FromSeconds(30));
    });
try
{

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowReact");

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();

}
catch (Exception ex)
{
    Console.WriteLine($"Error starting the application: {ex.Message}");
    throw;
}
