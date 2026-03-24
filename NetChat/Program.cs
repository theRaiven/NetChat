using static System.Console;
namespace NetChat;

class Program
{
    static async Task Main()
    {
        WriteLine("Выберите режим работы: 1 - Сервер, 2 - Клиент");
        string? mode = ReadLine();
        if (mode == "1")
        {
            ServerObject server = new ServerObject();
            await server.ListenAsync();
        }
        else if (mode == "2")
        {
            Write("Введите хост и порт сервера в формате \"10.241.82.201:8888\" соответственно: ");
            string? host = ReadLine();
            int port;

            if (host != null)
            {
                string[] parts = host.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int parsedPort))
                {
                    port = parsedPort;
                    host = parts[0];
                }
                else
                {
                    WriteLine("Неверный формат. Используйте формат 'хост:порт'.");
                    return;
                }
            }
            else
            {
                WriteLine("Хост не может быть пустым.");
                return;
            }

            Client client = new Client(host, port);
            await client.RunClient();
        }
        else
        {
            WriteLine("Неверный режим работы. Пожалуйста, выберите 1 или 2.");
        }
    }
}
