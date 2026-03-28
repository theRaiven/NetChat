using System.Text.Json;


namespace NetChat;

public class ChatMessage
{
    public string Type { get; set; } = "message"; // Тип сообщения 
    public string? User { get; set; } 
    public string? Text { get; set; }

}
