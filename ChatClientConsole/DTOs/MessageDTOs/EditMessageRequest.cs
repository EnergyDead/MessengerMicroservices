namespace ChatClientConsole.DTOs.MessageDTOs;

public class EditMessageRequest
{
    public Guid MessageId { get; set; }
    public string NewContent { get; set; } = string.Empty;
}