using Flurl.Http;
using Microsoft.AspNetCore.SignalR.Client;

static async Task ShowMessageFromSignalR(string msg)
{
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    if (msg == "[$ENDED$]")
    {
        await Console.Out.WriteLineAsync();
    }
    else
    {
        await Console.Out.WriteAsync(msg);
    }
    Console.ForegroundColor = ConsoleColor.White;
};

try
{
    var isStreaming = true;
    var trackingList = new List<RequestTracking>();
    List<string> questions =
    [
        "Greeting!",
        "Who are you?",
        "Tell me a joke",
        "What is the weather in London?",
        "What is the time?",
        //"How the stock market is doing?",
        //"Is a tomato a fruit or a vegetable?",
        //"What is the capital of France?",
    ];

    const string Host = "https://localhost:7279";
    var connection = new HubConnectionBuilder()
        .WithUrl($"{Host}/hub")
        .Build();

    connection.On<string>("AiResponse", ShowMessageFromSignalR);
    connection.On<string>("AiStreamResponse", ShowMessageFromSignalR);
    connection.On<ulong>("NextQueue", currentQueueNo =>
    {
        Console.WriteLine();
        trackingList.ForEach(it => it.Dequeue());
        trackingList.FirstOrDefault(it => it.QueueNo == currentQueueNo)?.TrackResponse();
        trackingList.ForEach(it => it.ShowStatus());
    });

    await connection.StartAsync();

    foreach (var question in questions)
    {
        var tracking = new RequestTracking { Question = question };
        trackingList.Add(tracking);
        var response = await $"{Host}/api/chat/{connection.ConnectionId}/{question}?streaming={isStreaming}".GetJsonAsync<AiResponse>();
        tracking.UpdateStatus(response);
    }

    await Console.In.ReadLineAsync();
    await connection.DisposeAsync();
    Console.WriteLine("Connection closed");
    await connection.StopAsync();
    await connection.DisposeAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

public sealed record AiResponse(string UserId, ulong QueueNo, ulong WaitingCount);

file class RequestTracking
{
    private bool _isSecondTime;
    private bool _gotResponse;
    public required string Question { get; init; }
    public ulong QueueNo { get; private set; }
    public ulong WaitingQueueCount { get; private set; }

    public void UpdateStatus(AiResponse response)
    {
        QueueNo = response.QueueNo;
        WaitingQueueCount = response.WaitingCount;
    }

    public void TrackResponse()
        => _gotResponse = true;

    public void Dequeue()
        => --WaitingQueueCount;

    public void ShowStatus()
    {
        var postfix = string.Empty;
        if (_gotResponse)
        {
            if (_isSecondTime)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else
            {
                _isSecondTime = true;
                Console.ForegroundColor = ConsoleColor.DarkGreen;
            }
        }
        else
        {
            postfix = $"[Waiting: {WaitingQueueCount}]";
            Console.ForegroundColor = ConsoleColor.DarkGray;
        }

        Console.WriteLine($"(Queue: {QueueNo}) {Question} {postfix}");
        Console.ForegroundColor = ConsoleColor.White;
    }
}