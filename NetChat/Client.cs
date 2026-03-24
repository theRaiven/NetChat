using System.Net.Sockets;
using static System.Console;

namespace NetChat;

class Client
{
    string Host { get; set; }
    int Port { get; set; }
    TcpClient client;
    string? UserName { get; set; }
    public Client(string host, int port)
    {
        Host = host;
        Port = port;
        client = new TcpClient();
    }
    public async Task RunClient()
    {
        Write("Введите имя пользователя: ");
        UserName = ReadLine();
        WriteLine($"Добро пожаловать, {UserName}");

        StreamReader? Reader = null;
        StreamWriter? Writer = null;

        var cts = new CancellationTokenSource();

        try
        {
            await client.ConnectAsync(Host, Port);  // подключаемся к серверу
            Reader = new StreamReader(client.GetStream());
            Writer = new StreamWriter(client.GetStream());
            if (Writer is null || Reader is null) return;

            // запускаем новый поток для получения данных
            var receiveTask = Task.Run(() => ReceiveMessageAsync(Reader, cts.Token));

            // запускаем ввод сообщений
            await SendMessageAsync(Writer);

            cts.Cancel();       // остановить Receive
            await receiveTask;  // дождаться завершения
        }
        catch (Exception ex)
        {
            WriteLine(ex.Message);
        }
        finally
        {
            Reader?.Close();
            Writer?.Close();
            client.Close();
        }
    }
    private async Task SendMessageAsync(StreamWriter Writer)
    {
        await Writer.WriteLineAsync(UserName);
        await Writer.FlushAsync();
        WriteLine("Для отправки сообщений введите сообщение и нажмите Enter");

        while (true)
        {
            string? message = ReadLine();

            if (string.IsNullOrWhiteSpace(message)) continue;
            if (message == "/exit") break;
            
            await Writer.WriteLineAsync(message);
            await Writer.FlushAsync();
        }
    }
    private async Task ReceiveMessageAsync(StreamReader Reader, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                string? message = await Reader.ReadLineAsync();
                if (string.IsNullOrEmpty(message)) continue;
                Print(message);
            }
            catch
            {
                break;
            }
        }
    }
    private void Print(string? message)
    {
        if (OperatingSystem.IsWindows())    // если ОС Windows
        {
            var position = GetCursorPosition(); // получаем текущую позицию курсора
            int left = position.Left;   // смещение в символах относительно левого края
            int top = position.Top;     // смещение в строках относительно верха
                                        // копируем ранее введенные символы в строке на следующую строку
            MoveBufferArea(0, top, left, 1, 0, top + 1);
            // устанавливаем курсор в начало текущей строки
            SetCursorPosition(0, top);
            // в текущей строке выводит полученное сообщение
            WriteLine(message);
            // переносим курсор на следующую строку
            // и пользователь продолжает ввод уже на следующей строке
            SetCursorPosition(left, top + 1);
        }
        else WriteLine(message);
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
            string? userName = await Reader.ReadLineAsync();
            string? message = $"{userName} вошел в чат";

            await server.BroadcastMessageAsync(message, Id);
            WriteLine(message);

            while (true)
            {
                try
                {
                    message = await Reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(message)) continue;
                    if (message == "/exit" || message == "/quit")
                    {
                        await Exit(message, userName);
                        break;
                    }

                    message = $"{userName}: {message}";
                    WriteLine(message);
                    await server.BroadcastMessageAsync(message, Id);
                }
                catch
                {
                    await Exit(message, userName);
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
    private async Task Exit(string? message, string? userName)
    {
        message = $"{userName} покинул чат";
        WriteLine(message);
        await server.BroadcastMessageAsync(message, Id);
    }
    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        client.Close();
    }
}
