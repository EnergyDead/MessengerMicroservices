namespace ChatClientConsole.DTOs.MessageDTOs;

public class ReadMessagesRequest
{
    public Guid ChatId { get; set; }
    public List<Guid> MessageIds { get; set; } = [];
}