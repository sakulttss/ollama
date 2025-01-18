# How to run the project
1. RabbitMQ
2. Ollama serve
3. ASP.NET server → **server** directory and run `dotnet run` command
4. React client → **web** directory and run `npm run dev` command

## How does it work?
### Server side
1. ตัวเซิฟเวอร์จะเปิด API เส้น `/api/chat` เพื่อให้ฝั่ง client สามารถส่ง prompt เข้ามาถาม AI ได้ โดยฝั่ง client จะต้องระบุ ClientID ของตัวเองมาด้วย เพื่อใช้กำหนดว่าเป็น UserId อะไร (ไฟล์ server/Program.cs บรรทัด 25)
```csharp
app.MapGet("/api/chat/{userId}/{prompt}", (string userId, string prompt, QueueService queue)
    => queue.Enqueue(new AiRequest(userId, prompt)));
```

> **NOTE**  
> API เส้นนี้จะรองรับการส่งข้อมูลเป็น streaming โดยให้ระบุ querystring มาเป็น `streaming=true` ซึ่งโดยปรกติจะมีค่าเป็น fault

2. เมื่อ API ถูกเรียก ระบบจะนำ promt ส่งไปเข้า RabbitMQ เพื่อรอให้ระบบส่ง prompt ไปยัง AI ได้ตรงตามลำดับก่อนหลัง (ไฟล์ server/Services/QueueService.cs บรรทัด 61)
```csharp
public ValueTask Enqueue(AiRequest request)
{
    ArgumentNullException.ThrowIfNull(_channel, nameof(_channel));
    var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
    return _channel.BasicPublishAsync(string.Empty, RoutingKey, message);
}
```


3. เมื่อ AI ตอบกลับมา ระบบจะส่งคำตอบกลับไปยัง client ผ่าน SignalR ที่ฝั่ง client connect ไว้ (ไฟล์ server/Services/QueueService.cs บรรทัด 46)
```csharp
await foreach (var item in chatClient.CompleteStreamingAsync(chatHistory))
{
    response.Append(item.Text);
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    await Console.Out.WriteAsync(item.Text);
    Console.ForegroundColor = ConsoleColor.White;
}
```

### Client side
1. เมื่อ client ถูกเปิดขึ้นมา มันจะทำการ connect ไปยัง SignalR ที่เซิฟเวอร์เพื่อขอ ClientID และช่องทางในการรับคำตอบจากเซิฟเวอร์ โดยทำงานผ่านเส้น `/hub` (ไฟล์ web/pages/index.js บรรทัด 14)
```js
const connection = new HubConnectionBuilder()
  .withUrl("https://localhost:7279/hub")
  .configureLogging(signalR.LogLevel.Information)
  .build();
```

2. เมื่อผู้ใช้ทำการกดปุ่มส่งคำถาม ระบบจะทำการส่งคำถามพร้อท ClientID ไปยังเซิฟเวอร์ผ่าน API ที่เปิดไว้ (ไฟล์ index.js บรรทัด 74)
```js
await fetch(`https://localhost:7279/api/chat/${connection.connectionId}/${encodeURIComponent(input)}`);
```

3. เมื่อ SignalR ส่งผลลัพท์กลับมา ฝั่ง Client จะนำข้อมูลที่ได้ไปแสดงผลที่ ChatBox (ไฟล์ index.js บรรทัด 31)
```js
connection.on("AiResponse", (message) => {
  const assistantMessage = { role: 'assistant', content: message };
  setMessages((prevMessages) => [...prevMessages, assistantMessage]);
});
```


[Video 1: Overall](https://www.loom.com/share/18cf01757eb446568f4862c0c0ca0e85?sid=aa97230e-baed-47fd-8ec7-0a7fb304cc52)

[Video 2: Performance testing](https://www.loom.com/share/68528a537b6a47d4b1c6d46c796ec336?sid=b531bf86-9a6b-4b45-913c-4b3f31eca74c)

[Video 3: Queue No & Realtime response](https://www.loom.com/share/b76533697c954bd29f65ebcc0961a8fa?sid=da296aea-b2e6-443d-815d-ce7f9c71e51d)

[Video 4: Show waiting queue](https://www.loom.com/share/26b3bdaa6ede4fd9b948c88eb93150cd?sid=15181f09-f872-44ca-8da8-bfbd0971c061)