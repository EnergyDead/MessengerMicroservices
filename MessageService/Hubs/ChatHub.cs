using System.Text.Json;
using MessageService.Data;
using MessageService.DTOs;
using MessageService.Models;
using Microsoft.AspNetCore.SignalR;

namespace MessageService.Hubs;

public class ChatHub: Hub
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private const string ChatServiceUrl = "http://localhost:5000"; // todo: вынести в настройки

    public ChatHub(AppDbContext db)
    {
        _db = db;
        _httpClient = new HttpClient();
    }
    
    /// <summary>
    /// Отправляет сообщение в указанный чат.
    /// </summary>
    public async Task SendMessage(Guid chatId, Guid senderId, string content)
    {
        var chatExists = await CheckChatExists(chatId);
        if (!chatExists)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Chat not found.");
            return;
        }

        var isParticipant = await CheckUserIsParticipant(chatId, senderId);
        if (!isParticipant)
        {
            await Clients.Caller.SendAsync("ReceiveError", "You are not a participant of this chat.");
            return;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        await Clients.Group(chatId.ToString()).SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Метод, вызываемый при подключении клиента к хабу.
    /// Клиент должен присоединиться к группам чатов, в которых он состоит.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // todo: добавлять пользователя в группы.
        // получить ID пользователя
        // из контекста аутентификации (Context.User.Identity.Name или из JWT-токена)

        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// Метод, который клиент должен вызвать, чтобы присоединиться к группе чата.
    /// Это необходимо, чтобы получать сообщения для этого чата.
    /// </summary>
    public async Task JoinChatGroup(Guid chatId, Guid userId)
    {
        var isParticipant = await CheckUserIsParticipant(chatId, userId);
        if (!isParticipant)
        {
            await Clients.Caller.SendAsync("ReceiveError", "You are not authorized to join this chat group.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, chatId.ToString());
        await Clients.Caller.SendAsync("ReceiveInfo", $"Joined chat group: {chatId}");
    }
    
    public async Task LeaveChatGroup(Guid chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());
        await Clients.Caller.SendAsync("ReceiveInfo", $"Left chat group: {chatId}");
    }
    
    private async Task<bool> CheckChatExists(Guid chatId)
    {
        var response = await _httpClient.GetAsync($"{ChatServiceUrl}/api/chats/{chatId}");
        return response.IsSuccessStatusCode;
    }
    private async Task<bool> CheckUserIsParticipant(Guid chatId, Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ChatServiceUrl}/api/chats/{chatId}");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatServiceChatResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return chatResponse?.ParticipantIds?.Contains(userId) ?? false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking participant: {ex.Message}");
            return false;
        }
    }
}