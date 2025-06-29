namespace ChatService.Models;

public class Chat
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; }
    public ICollection<UserChat> Participants { get; set; } = new List<UserChat>();
}