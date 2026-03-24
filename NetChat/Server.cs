using System.Net;
using System.Net.Sockets;
using static System.Console;
namespace NetChat;

class ServerObject
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // сервер для прослушивания
    readonly List<ClientObject> clients = new List<ClientObject>(); // все подключения
    readonly object locker = new();

    protected internal void RemoveConnection(string id)
    {
        ClientObject? client;

        lock (locker)
        {
            client = clients.FirstOrDefault(c => c.Id == id);
            if (client != null)
                clients.Remove(client);
        }

        client?.Close();
    }
    protected internal async Task ListenAsync()
    {
        try
        {
            tcpListener.Start();
            WriteLine("Сервер запущен. Ожидание подключений...");

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

        lock (locker)
        {
            clientsCopy = clients.ToList();
        }

        foreach (var client in clientsCopy)
        {
            if (client.Id != id)
            {
                try
                {
                    await client.Writer.WriteLineAsync(message);
                    await client.Writer.FlushAsync();
                }
                catch
                {
                    RemoveConnection(client.Id);
                }
            }
        }
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
            client.Close();
        }
        tcpListener.Stop(); //остановка сервера
    }
}