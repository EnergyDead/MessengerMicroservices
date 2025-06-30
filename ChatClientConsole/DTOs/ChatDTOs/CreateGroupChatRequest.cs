namespace ChatClientConsole.DTOs.ChatDTOs;

public class CreateGroupChatRequest
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> ParticipantIds { get; set; } = [];
}