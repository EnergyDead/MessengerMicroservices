namespace MessageService.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
}