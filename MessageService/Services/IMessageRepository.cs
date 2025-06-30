using MessageService.DTOs;
using MessageService.Models;

namespace MessageService.Services;

public interface IMessageRepository
{
    Task<(bool success, string? errorMessage, Message? message)> CreateMessageAsync(Guid chatId, Guid senderId, string content);
    Task<(bool success, string? errorMessage, Message? updatedMessage)> EditMessageAsync(EditMessageRequest request);
    Task<(bool success, string? errorMessage)> DeleteMessageAsync(Guid messageId, Guid deleterId);
    Task<Message?> GetMessageByIdAsync(Guid messageId);
    Task<IEnumerable<Message>> GetMessagesByChatIdAsync(Guid chatId);
}