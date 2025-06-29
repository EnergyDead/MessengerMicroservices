namespace ChatService.Models;

public class UserChat
{
    public Guid ChatId { get; set; }
    public Guid UserId { get; set; }

    public Chat Chat { get; set; } = null!;
}