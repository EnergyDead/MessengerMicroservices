namespace ChatService.DTOs;

public class CreateGroupChatRequest
{
    public string Name { get; set; } = null!;
    public List<Guid> ParticipantIds { get; set; } = [];
}