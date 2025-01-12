using server.Models;
using server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatHub>();
builder.Services.AddSingleton<QueueService>();
builder.Services.AddCors(setup =>
{
    setup.AddDefaultPolicy(cfg =>
    {
        cfg
        .WithOrigins("http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});
var app = builder.Build();
await app.Services.CreateScope().ServiceProvider.GetRequiredService<QueueService>().Initialize();

app.UseHttpsRedirection();
app.UseCors();
app.MapHub<ChatHub>("/hub");
app.MapGet("/api/chat/{userId}/{prompt}", (string userId, string prompt, QueueService queue)
    => queue.Enqueue(new AiRequest(userId, prompt)));

app.Run();