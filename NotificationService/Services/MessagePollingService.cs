using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Constants;
using NotificationService.Data;
using NotificationService.DTOs;

namespace NotificationService.Services;

public class MessagePollingService : IMessagePollingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationDbContext _dbContext;

    public MessagePollingService(IHttpClientFactory httpClientFactory,
        NotificationDbContext dbContext)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<MessageDto>> GetUnprocessedMessagesAsync(DateTimeOffset lastCheckedTimestamp)
    {
        var httpClient = _httpClientFactory.CreateClient(ServiceConstants.MessageServiceHttpClientName);
        var response = await httpClient.GetAsync(
            $"{ServiceConstants.MessageServiceHttpClientName}{ServiceConstants.MessageServiceMessagesSincePath}{lastCheckedTimestamp.ToUnixTimeMilliseconds()}");

        if (!response.IsSuccessStatusCode) return [];
        var json = await response.Content.ReadAsStringAsync();
        var messages = JsonSerializer.Deserialize<List<MessageDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return messages ?? [];
    }

    public async Task UpdateMessageReadStatusAsync(Guid messageId, Guid recipientId, DateTimeOffset readTimestamp)
    {
        var notification = await _dbContext.MessageNotifications
            .FirstOrDefaultAsync(mn => mn.MessageId == messageId && mn.RecipientId == recipientId && !mn.IsRead);

        notification.IsRead = true;
        notification.ReadTimestamp = readTimestamp;
        _dbContext.MessageNotifications.Update(notification);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsUserOnlineAsync(Guid userId)
    {
        var httpClient = _httpClientFactory.CreateClient(ServiceConstants.MessageServiceHttpClientName);
        var response = await httpClient.GetAsync(
            $"{ServiceConstants.MessageServiceBaseApiPath}{ServiceConstants.MessageServiceUsersOnlinePath}{userId}");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return bool.Parse(json);
        }

        return false;
    }

    public async Task<List<Guid>> GetChatParticipantsAsync(Guid chatId)
    {
        var httpClient = _httpClientFactory.CreateClient(ServiceConstants.UserServiceHttpClientName);
        var response = await httpClient.GetAsync($"{ServiceConstants.ChatServiceBaseApiPath}/{chatId}");

        if (!response.IsSuccessStatusCode) return [];
        var json = await response.Content.ReadAsStringAsync();
        var chatInfo = JsonSerializer.Deserialize<ChatInfoResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return chatInfo?.ParticipantIds ?? [];
    }
}