using MessageService.Data;
using MessageService.DTOs;
using MessageService.Models;
using Microsoft.EntityFrameworkCore;

namespace MessageService.Services;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _db;

    public MessageRepository(AppDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Message?> GetMessageByIdAsync(Guid messageId)
    {
        return await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
    }

    public async Task<IEnumerable<Message>> GetMessagesByChatIdAsync(Guid chatId)
    {
        return await _db.Messages
            .Where(m => m.ChatId == chatId && !m.IsDeleted)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<Message> CreateMessageAsync(Message message)
    {
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    public async Task<(bool Success, string ErrorMessage, Message? UpdatedMessage)> EditMessageAsync(
        EditMessageRequest request)
    {
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == request.MessageId);

        if (message == null)
        {
            return (false, $"Message with ID {request.MessageId} not found.", null);
        }

        // Проверка, что редактировать может только отправитель
        if (message.SenderId != request.EditorId)
        {
            return (false, "Only the sender can edit their message.", null);
        }

        message.Content = request.NewContent;
        message.IsEdited = true;
        message.Timestamp = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            return (true, string.Empty, message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Concurrency conflict while saving changes. Please try again.", null);
        }
        catch (Exception ex)
        {
            return (false, $"An error occurred while editing the message: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteMessageAsync(Guid messageId, Guid deleterId)
    {
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            return (false, $"Message with ID {messageId} not found.");
        }

        if (message.SenderId != deleterId)
        {
            return (false, "Only the sender can delete their message.");
        }

        message.IsDeleted = true;
        message.Content = "[Сообщение удалено]";
        message.IsEdited = true; 
        message.Timestamp = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            return (true, string.Empty);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Concurrency conflict while saving changes. Please try again.");
        }
        catch (Exception ex)
        {
            return (false, $"An error occurred while deleting the message: {ex.Message}");
        }
    }
}