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
        _db = db;
    }

    public async Task<(bool success, string? errorMessage, Message? message)> CreateMessageAsync(Guid chatId,
        Guid senderId,
        string content)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        return (true, null, message);
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

    public async Task<(bool success, string? errorMessage, Message? updatedMessage)> EditMessageAsync(
        EditMessageRequest request)
    {
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == request.MessageId);

        if (message == null)
        {
            return (false, "Message not found.", null);
        }

        if (message.SenderId != request.EditorId)
        {
            return (false, "Only the sender can edit this message.", null);
        }

        if (message.IsDeleted)
        {
            return (false, "Cannot edit a deleted message.", null);
        }

        message.Content = request.NewContent;
        message.Timestamp = DateTimeOffset.UtcNow;

        _db.Messages.Update(message);
        await _db.SaveChangesAsync();

        return (true, null, message);
    }

    public async Task<(bool success, string? errorMessage)> DeleteMessageAsync(Guid messageId,
        Guid deleterId)
    {
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            return (false, "Message not found.");
        }

        if (message.SenderId != deleterId)
        {
            return (false, "Only the sender can delete this message.");
        }

        if (message.IsDeleted)
        {
            return (false, "Message is already marked as deleted.");
        }

        message.IsDeleted = true;

        _db.Messages.Update(message);
        await _db.SaveChangesAsync();

        return (true, null);
    }
}