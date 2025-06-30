namespace ChatClientConsole.DTOs.ChatDTOs;

public class ChatResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string? Name { get; set; }
    public List<Guid> ParticipantIds { get; set; } = [];
}