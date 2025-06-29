namespace ChatService.DTOs;

public class CreatePersonalChatRequest
{
    public Guid User1Id { get; set; }
    public Guid User2Id { get; set; }
}