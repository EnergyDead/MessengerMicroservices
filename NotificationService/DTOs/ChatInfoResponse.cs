namespace NotificationService.DTOs;

public class ChatInfoResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string? Name { get; set; }
    public List<Guid> ParticipantIds { get; set; } = [];
}