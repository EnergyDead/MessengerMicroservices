namespace MessageService.DTOs;

public class MessageResponse
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
}