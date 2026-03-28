using System.Threading.Channels;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using static System.Console;

namespace NetChat;

class ServerObject
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // сервер для прослушивания
    readonly List<ClientObject> clients = new List<ClientObject>(); // все подключения
    readonly Channel<(string message, string senderId)> messageQueue =
        Channel.CreateUnbounded<(string message, string senderId)>();

    readonly object locker = new();

    X509Certificate2 certificate;
    public ServerObject()
    {
        certificate = new X509Certificate2("server.pfx", "password");
    }
    protected internal void EnqueueMessage(string message, string senderId)
    {
        messageQueue.Writer.TryWrite((message, senderId));
    }
    protected internal async Task ProcessMessageQueueAsync()
    {
        await foreach (var item in messageQueue.Reader.ReadAllAsync())
        {
            await BroadcastMessageAsync(item.message, item.senderId);
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

                ClientObject clientObject = new ClientObject(tcpClient, this, certificate);

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
    X509Certificate2 certificate;
    public ClientObject(TcpClient tcpClient, ServerObject serverObject, X509Certificate2 cert)
    {
        client = tcpClient;
        server = serverObject;
        certificate = cert;

        var sslStream = new SslStream(client.GetStream(), false);

        sslStream.AuthenticateAsServer(
            certificate,
            clientCertificateRequired: false,
            enabledSslProtocols: SslProtocols.Tls13,
            checkCertificateRevocation: false);

        Reader = new StreamReader(sslStream);
        Writer = new StreamWriter(sslStream)
        {
            AutoFlush = true
        };

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