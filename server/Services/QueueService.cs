using Microsoft.Extensions.AI;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using server.Models;
using System.Text;
using System.Text.Json;

namespace server.Services;

public class QueueService : IAsyncDisposable
{
    private IChannel? _channel;
    private IConnection? _connection;
    private readonly ChatHub _hub;
    private readonly IConfiguration _configuration;
    private const string RoutingKey = "AiRequest";

    public QueueService(ChatHub hub, IConfiguration configuration)
    {
        _hub = hub;
        _configuration = configuration;
    }

    public async Task Initialize()
    {
        var queueServerHost = _configuration
            .GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException();
        var factory = new ConnectionFactory() { Uri = new Uri(queueServerHost) };
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
        var consumer = new AsyncEventingBasicConsumer(_channel);
        var aiServer = _configuration
            .GetSection(nameof(AiServer))
            .Get<AiServer>()
            ?? throw new ArgumentNullException();
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var request = JsonSerializer.Deserialize<AiRequest>(message)
                ?? throw new ArgumentNullException();
            await Console.Out.WriteLineAsync($"Working with user: {request.UserId}, prompt: '{request.Prompt}'");
            using var chatClient = new OllamaChatClient(new Uri(aiServer.Host), aiServer.Model);
            var chatHistory = ChatHub.GetChatMessages(request);
            StringBuilder response = new();
            await foreach (var item in chatClient.CompleteStreamingAsync(chatHistory))
            {
                response.Append(item.Text);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                await Console.Out.WriteAsync(item.Text);
                Console.ForegroundColor = ConsoleColor.White;
            }
            await Console.Out.WriteLineAsync();
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.ToString()));
            await _hub.SendMessageToClient(request.UserId, response.ToString());
        };
        await _channel.BasicConsumeAsync(RoutingKey, autoAck: true, consumer: consumer);
        await _channel.QueueDeclareAsync(RoutingKey, false, false, false);
    }

    public ValueTask Enqueue(AiRequest request)
    {
        ArgumentNullException.ThrowIfNull(_channel, nameof(_channel));
        var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        return _channel.BasicPublishAsync(string.Empty, RoutingKey, message);
    }

    public async ValueTask DisposeAsync()
    {
        ArgumentNullException.ThrowIfNull(_channel, nameof(_channel));
        ArgumentNullException.ThrowIfNull(_connection, nameof(_connection));
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}