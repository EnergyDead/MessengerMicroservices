using NotificationService.DTOs;

namespace NotificationService.Services;

public interface IMessagePollingService
{
    Task<List<MessageDto>> GetUnprocessedMessagesAsync(DateTimeOffset lastCheckedTimestamp);
    Task UpdateMessageReadStatusAsync(Guid messageId, Guid recipientId, DateTimeOffset readTimestamp);
    Task<bool> IsUserOnlineAsync(Guid userId);
    Task<List<Guid>> GetChatParticipantsAsync(Guid chatId);
}