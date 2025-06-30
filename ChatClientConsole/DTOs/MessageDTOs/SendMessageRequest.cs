namespace ChatClientConsole.DTOs.MessageDTOs;

public class SendMessageRequest
{
    public Guid ChatId { get; set; }
    public string Content { get; set; } = string.Empty;
}