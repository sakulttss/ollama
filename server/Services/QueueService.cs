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
    private string RoutingKey = Guid.NewGuid().ToString();
    private static ulong DeliveryCount = 0;

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
        _channel = await _connection.CreateChannelAsync(new(true, false));
        await _channel.QueueDeclareAsync(RoutingKey);

        var aiServer = _configuration
            .GetSection(nameof(AiServer))
            .Get<AiServer>()
            ?? throw new ArgumentNullException();

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var request = JsonSerializer.Deserialize<AiRequest>(message)
                ?? throw new ArgumentNullException();
            if (!_hub.ClientExists(request.UserId))
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                await Console.Out.WriteLineAsync($"[REJECTED] User not existing: {request.UserId}, prompt: {request.Prompt}");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            DeliveryCount++;
            await Console.Out.WriteLineAsync($"(Queue:{DeliveryCount}) Working with user: {request.UserId}, prompt: '{request.Prompt}'");
            using var chatClient = new OllamaChatClient(new Uri(aiServer.Host), aiServer.Model);
            var chatHistory = ChatHub.GetChatMessages(request);
            StringBuilder response = new();
            await foreach (var item in chatClient.CompleteStreamingAsync(chatHistory))
            {
                response.Append(item.Text);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                await Console.Out.WriteAsync(item.Text);
                if (request.RequiredStreaming)
                {
                    await _hub.SendStreamMessageToClient(request.UserId, item.Text);
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
            await Console.Out.WriteLineAsync();
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, response.ToString()));
            if (request.RequiredStreaming)
            {
                await _hub.SendStreamMessageToClient(request.UserId, "[$ENDED$]");
            }
            else
            {
                await _hub.SendMessageToClient(request.UserId, response.ToString());
            }
        };
        await _channel.BasicConsumeAsync(RoutingKey, true, consumer);
    }

    public async Task<AiResponse> Enqueue(AiRequest request)
    {
        ArgumentNullException.ThrowIfNull(_channel, nameof(_channel));
        var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        var queueNo = await _channel.GetNextPublishSequenceNumberAsync();
        var waitingCount = (queueNo == 1) ? 0 : (queueNo) - DeliveryCount;
        await _channel.BasicPublishAsync(string.Empty, RoutingKey, message);
        return new AiResponse(request.UserId, queueNo, waitingCount);
    }

    public async ValueTask DisposeAsync()
    {
        ArgumentNullException.ThrowIfNull(_channel, nameof(_channel));
        ArgumentNullException.ThrowIfNull(_connection, nameof(_connection));
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}