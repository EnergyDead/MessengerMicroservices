using System.ComponentModel.DataAnnotations;

namespace NotificationService.Models;

public class MessageNotification
{
    [Key] public Guid Id { get; set; }

    public Guid MessageId { get; set; }
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }

    public DateTimeOffset SentTimestamp { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTimeOffset? ReadTimestamp { get; set; }

    public bool IsEmailSent { get; set; } = false;
    public DateTimeOffset? EmailSentTimestamp { get; set; }
}