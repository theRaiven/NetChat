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

        try
        {
            client.Connect(Host, Port); // подключаемся к серверу
            Reader = new StreamReader(client.GetStream());
            Writer = new StreamWriter(client.GetStream());
            if (Writer is null || Reader is null) return;

            // запускаем новый поток для получения данных
            _ = Task.Run(() => ReceiveMessageAsync(Reader));

            // запускаем ввод сообщений
            await SendMessageAsync(Writer);
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
            if (message is null) break;
            await Writer.WriteLineAsync(message);
            await Writer.FlushAsync();
        }
    }
    private async Task ReceiveMessageAsync(StreamReader Reader)
    {
        while (true)
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
                    if (message is null)
                    {
                        break;
                    }
                    message = $"{userName}: {message}";
                    WriteLine(message);
                    await server.BroadcastMessageAsync(message, Id);
                }
                catch
                {
                    message = $"{userName} покинул чат";
                    WriteLine(message);
                    await server.BroadcastMessageAsync(message, Id);
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
    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        client.Close();
    }
}
