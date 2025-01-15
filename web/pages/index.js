import React, { useState, useEffect } from 'react';
import Head from 'next/head';
import { HubConnectionBuilder } from '@microsoft/signalr';

const ChatComponent = () => {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [connection, setConnection] = useState(null);
  const [isStreaming, setIsStreaming] = useState(false);

  useEffect(() => {
    const connectSignalR = async () => {
      const connection = new HubConnectionBuilder()
        .withUrl("https://localhost:7279/hub")
        .configureLogging(signalR.LogLevel.Information)
        .build();
      try {
        await connection.start();
        console.log("SignalR Connected.");
        console.log("Client ID:", connection.connectionId);
        setConnection(connection);
      } catch (err) {
        console.log(err);
        setTimeout(connectSignalR, 5000);
      }

      connection.onclose(async () => {
        await connectSignalR();
      });

      connection.on("AiStreamResponse", (message) => {
        if (message === "[$ENDED$]") {
          setLoading(false);
          return;
        }

        setMessages((prevMessages) => {
          const lastMessage = prevMessages[prevMessages.length - 1];
          if (lastMessage && lastMessage.role === 'assistant') {
            // Append the new text to the last assistant message
            const updatedMessage = { ...lastMessage, content: lastMessage.content + message };
            return [...prevMessages.slice(0, -1), updatedMessage];
          } else {
            // Add a new assistant message if there is no previous assistant message
            const assistantMessage = { role: 'assistant', content: message };
            return [...prevMessages, assistantMessage];
          }
        });
      });

      connection.on("AiResponse", (message) => {
        setLoading(false);
        setMessages((prevMessages) => [
          ...prevMessages,
          { role: 'assistant', content: message }
        ]);
      });
    };

    connectSignalR();
  }, []);

  const handleSend = async () => {
    const userMessage = { role: 'user', content: input };
    setMessages([...messages, userMessage]);
    setInput('');
    setLoading(true);

    try {
      await fetch(`https://localhost:7279/api/chat/${connection.connectionId}/${encodeURIComponent(input)}?streaming=${isStreaming}`);
    } catch (error) {
      console.error('Error fetching message:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (event) => {
    if (event.key === 'Enter') {
      handleSend();
    }
  };

  return (
    <div>
      <Head>
        <title>Ollama Chat</title>
        <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
        <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/6.0.1/signalr.js"></script>
      </Head>
      <div className="chat-container">
        <div className="messages">
          {messages.map((msg, index) => (
            <div key={index} className={`message ${msg.role}`}>
              {msg.content}
            </div>
          ))}
        </div>
        {loading && <div className="loading-indicator">Generating...</div>}
        <div className="input-container">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          <button onClick={handleSend}>Send</button>
          <label>
            <input
              type="checkbox"
              checked={isStreaming}
              onChange={(e) => setIsStreaming(e.target.checked)}
            />
            Streaming
          </label>
        </div>
      </div>
      <style jsx>{`
        .chat-container {
          width: 80%;
          margin: 0 auto;
          padding-top: 50px;
        }
        .messages {
          border: 1px solid #ccc;
          padding: 10px;
          height: 400px;
          overflow-y: scroll;
        }
        .message {
          margin-bottom: 10px;
        }
        .message.user {
          text-align: right;
          color: blue;
        }
        .message.assistant {
          text-align: left;
          color: green;
        }
        .input-container {
          display: flex;
          margin-top: 10px;
        }
        input {
          flex: 1;
          padding: 10px;
          font-size: 16px;
        }
        button {
          padding: 10px 20px;
          font-size: 16px;
        }
        .loading-indicator {
          text-align: center;
          margin-top: 10px;
          font-size: 14px;
          color: gray;
        }
      `}</style>
    </div>
  );
};

export default ChatComponent;
