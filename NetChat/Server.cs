using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Collections.Concurrent;
using static System.Console;
namespace NetChat;

class ServerObject
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // сервер для прослушивания
    readonly List<ClientObject> clients = new List<ClientObject>(); // все подключения
    readonly ConcurrentQueue<(string message, string senderId)> messageQueue = new();
    readonly object locker = new();

    protected internal void EnqueueMessage(string message, string senderId)
    {
        messageQueue.Enqueue((message, senderId));
    }
    protected internal async Task ProcessMessageQueueAsync()
    {
        while (true)
        {
            if (messageQueue.TryDequeue(out var item))
            {
                await BroadcastMessageAsync(item.message, item.senderId);
            }
            else
            {
                await Task.Delay(10); // небольшая задержка для снижения нагрузки
            }
        }
    }
    protected internal async Task ListenAsync()
    {
        try
        {
            tcpListener.Start();
            WriteLine("Сервер запущен. Ожидание подключений...");

            _ = Task.Run(ProcessMessageQueueAsync);

            while (true)
            {
                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                
                WriteLine($"Подключен клиент {tcpClient.Client.RemoteEndPoint}");
                
                ClientObject clientObject = new ClientObject(tcpClient, this);
                lock (locker)
                {
                    clients.Add(clientObject);
                    
                }
                _ = Task.Run(() => clientObject.ProcessAsync());
            }
        }
        catch (Exception ex)
        {
            WriteLine(ex.Message);

        }
        finally
        {
            Disconnect();
        }
    }
    protected internal async Task BroadcastMessageAsync(string message, string id)
    {
        List<ClientObject> clientsCopy;
        List<string> disconnected = new();

        lock (locker)
        {
            clientsCopy = clients.ToList();
        }

        foreach (var client in clientsCopy)
        {
            if (client.Id == id)
                continue;

            try
            {
                await client.Writer.WriteLineAsync(message);
                await client.Writer.FlushAsync();
            }
            catch
            {
                disconnected.Add(client.Id);
            }
        }

        foreach (var clientId in disconnected)
        {
            WriteLine($"Клиент {clientId} отключён");
            RemoveConnection(clientId);
        }
    }

    protected internal void RemoveConnection(string id)
    {
        ClientObject? client;

        lock (locker)
        {
            client = clients.FirstOrDefault(c => c.Id == id);
            if (client != null)
            {
                WriteLine($"Удаление клиента {id}");
                clients.Remove(client);
            }
        }

        client?.Close();
    }
    protected internal void Disconnect()
    {
        List<ClientObject> clientsCopy;

        lock (locker)
        {
            clientsCopy = clients.ToList();
        }

        foreach (var client in clientsCopy)
        {
            WriteLine($"Отключение клиента {client.Id}");
            client.Close();
        }
        tcpListener.Stop(); //остановка сервера
    }
}
class ClientObject
{
    protected internal string Id { get; private set; } = Guid.NewGuid().ToString();
    protected internal StreamWriter Writer { get; private set; }
    protected internal StreamReader Reader { get; private set; }

    TcpClient client;
    ServerObject server;

    public ClientObject(TcpClient tcpClient, ServerObject serverObject)
    {
        client = tcpClient;
        server = serverObject;

        var stream = client.GetStream();  // получаем NetworkStream для взаимодействия с сервером
        Reader = new StreamReader(stream);
        Writer = new StreamWriter(stream);
    }
    public async Task ProcessAsync()
    {
        try
        {
            ChatMessage? joinMsg = await ReadMessage();
            if (joinMsg?.Type != "join") return;

            var joinMessage = CreateSystemMessage(type: "join",
                                                  user: joinMsg.User,
                                                  text: $"{joinMsg.User} вошел в чат");

            await Broadcast(joinMessage);


            while (true)
            {
                try
                {
                    ChatMessage? msg = await ReadMessage();
                    if (msg == null) continue;

                    switch (msg.Type)
                    {
                        case "message":
                            WriteLine($"{msg.User}: {msg.Text}");
                            await Broadcast(msg);
                            break;

                        case "exit":
                            await Exit(msg.User);
                            return;
                    }
                }
                catch
                {
                    await Exit(joinMessage.User);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            WriteLine(ex.Message);
        }
        finally
        {
            server.RemoveConnection(Id);
            Close();
        }
    }

    private ChatMessage CreateSystemMessage(string type, string? user, string text)
    {
        return new ChatMessage
        {
            Type = type,
            User = user,
            Text = text
        };
    }
    private Task Broadcast(ChatMessage msg)
    {
        server.EnqueueMessage(
            JsonSerializer.Serialize(msg),
            Id);

        return Task.CompletedTask;
    }
    private async Task<ChatMessage?> ReadMessage()
    {
        string? json = await Reader.ReadLineAsync();
        if (json == null) throw new Exception("Client disconnected");

        if (string.IsNullOrWhiteSpace(json)) return null;

        return JsonSerializer.Deserialize<ChatMessage>(json);
    }

    private async Task Exit(string? userName)
    {
        WriteLine($"[{DateTime.Now:T}] {userName} покинул чат");

        var exitMessage = CreateSystemMessage(
            "exit",
            userName,
            $"{userName} покинул чат");

        await Broadcast(exitMessage);
    }
    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        client.Close();
    }
}