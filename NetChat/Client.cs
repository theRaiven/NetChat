using System.Net.Sockets;
using static System.Console;
using System.Text.Json;

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
            
            var stream = client.GetStream();

            Reader = new StreamReader(stream);
            Writer = new StreamWriter(stream);

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
        var joinMsg = new ChatMessage
        {
            Type = "join",
            User = UserName
        };
        string jsonJoin = JsonSerializer.Serialize(joinMsg);

        await Writer.WriteLineAsync(jsonJoin);
        await Writer.FlushAsync();

        WriteLine("\tДля отправки сообщений введите сообщение и нажмите Enter");
        WriteLine("\tДля выхода введите /exit");
        while (true)
        {
            Write(">>> ");
            string? message = ReadLine();

            if (string.IsNullOrWhiteSpace(message)) continue;
            if (message == "/exit")
            {
                ChatMessage exitMessage = new ChatMessage { Type = "exit" , User = UserName };

                await Writer.WriteLineAsync(JsonSerializer.Serialize(exitMessage));
                await Writer.FlushAsync();
                break;
            } 
                
            
            ChatMessage msg = new ChatMessage { Type = "message", User = UserName, Text = message };
            string jsonMsg = JsonSerializer.Serialize(msg);
            
            await Writer.WriteLineAsync(jsonMsg);
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

                ChatMessage? chatMessage = JsonSerializer.Deserialize<ChatMessage>(message);
                if (chatMessage == null) continue;
                Print($"{chatMessage.User}: {chatMessage.Text}");
            }
            catch
            {
                break;
            }
        }
    }
    private void Print(string? message)
    {
        if (OperatingSystem.IsWindows())    
        {
            var position = GetCursorPosition(); 
            int left = position.Left;   
            int top = position.Top;     
                                        
            MoveBufferArea(0, top, left, 1, 0, top + 1);
            // устанавливаем курсор в начало текущей строки
            SetCursorPosition(0, top);
            // в текущей строке выводит полученное сообщение
            WriteLine(message);
            // переносим курсор на следующую строкуи пользователь продолжает ввод уже на следующей строке
            SetCursorPosition(left, top + 1);
        }
        else WriteLine(message);
    }
}
