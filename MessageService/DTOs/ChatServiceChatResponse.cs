namespace MessageService.DTOs;

public class ChatServiceChatResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public List<Guid>? ParticipantIds { get; set; }
}