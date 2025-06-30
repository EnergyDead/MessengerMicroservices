using MessageService.DTOs;
using MessageService.Models;

namespace MessageService.Services;

public interface IMessageRepository
{
    Task<Message?> GetMessageByIdAsync(Guid messageId);
    Task<IEnumerable<Message>> GetMessagesByChatIdAsync(Guid chatId);
    Task<Message> CreateMessageAsync(Message message);
    Task<(bool Success, string ErrorMessage, Message? UpdatedMessage)> EditMessageAsync(EditMessageRequest request);
    Task<(bool Success, string ErrorMessage)> DeleteMessageAsync(Guid messageId, Guid deleterId);

}