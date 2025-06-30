using System.Security.Claims;
using System.Text.Json;
using MessageService.Constants;
using MessageService.DTOs;
using MessageService.Models;
using MessageService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MessageService.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly HttpClient _chatHttpClient;
    private readonly IUserPresenceService _presenceService;
    private readonly IMessageRepository _messageRepository;

    public ChatHub(IUserPresenceService presenceService,
        IHttpClientFactory httpClientFactory,
        IMessageRepository messageRepository)
    {
        _presenceService = presenceService ?? throw new ArgumentNullException(nameof(presenceService));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _chatHttpClient = httpClientFactory.CreateClient(ServiceConstants.ChatServiceHttpClientName);
    }

    /// <summary>
    /// Отправляет сообщение в указанный чат.
    /// </summary>
    public async Task SendMessage(SendMessageRequest request)
    {
        var senderId = GetCurrentUserId(); // ✨ Получаем senderId из токена
        
        // ВНИМАНИЕ: Здесь нужно будет проверить, что senderId является участником chatId.
        // Эту проверку, вероятно, должен будет выполнять ChatService или MessageRepository,
        // если он сможет получать информацию о чатах. Пока оставляем без этой проверки здесь.

        var (success, errorMessage, message) = await _messageRepository.CreateMessageAsync(
            request.ChatId, senderId, request.Content);

        if (success && message != null)
        {
            // Отправляем сообщение всем участникам чата
            await Clients.Group(request.ChatId.ToString()).SendAsync("ReceiveMessage", new MessageResponse
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Content = message.Content,
                Timestamp = message.Timestamp
            });
            // TODO: Публикация события "MessageCreated" в брокер сообщений для NotificationService
        }
        else
        {
            // Отправляем ошибку только отправителю
            await Clients.Caller.SendAsync("SendMessageError", errorMessage);
        }
    }

    /// <summary>
    /// Метод, вызываемый при подключении клиента к хабу.
    /// Клиент должен присоединиться к группам чатов, в которых он состоит.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        await _presenceService.UserConnected(userId, Context.ConnectionId);
        await Clients.All.SendAsync("UserStatusChanged", userId, true);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        await _presenceService.UserDisconnected(Context.ConnectionId);
        if (!await _presenceService.IsUserOnline(userId))
        {
            await Clients.All.SendAsync("UserStatusChanged", userId, false);
        }
        await base.OnDisconnectedAsync(exception);
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

        await _presenceService.UserConnected(userId, Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, chatId.ToString());
        await Clients.Caller.SendAsync("ReceiveInfo", $"Joined chat group: {chatId}");

        await Clients.Group(chatId.ToString()).SendAsync("UserStatusChanged", userId, true);
    }

    public async Task LeaveChatGroup(Guid chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());
        await Clients.Caller.SendAsync("ReceiveInfo", $"Left chat group: {chatId}");
    }

    public async Task<bool> IsUserOnline(Guid userId)
    {
        return await _presenceService.IsUserOnline(userId);
    }

    /// <summary>
    /// Клиент вызывает этот метод для редактирования сообщения.
    /// </summary>
    public async Task EditMessage(EditMessageRequest request)
    {
        var editorId = GetCurrentUserId();
        request.EditorId = editorId;

        var (success, errorMessage, updatedMessage) = await _messageRepository.EditMessageAsync(request);

        if (success && updatedMessage != null)
        {
            await Clients.Group(updatedMessage.ChatId.ToString()).SendAsync("MessageEdited", new MessageResponse
            {
                Id = updatedMessage.Id,
                ChatId = updatedMessage.ChatId,
                SenderId = updatedMessage.SenderId,
                Content = updatedMessage.Content,
                Timestamp = updatedMessage.Timestamp
            });
            // TODO: Публикация события "MessageEdited" в брокер сообщений
        }
        else
        {
            await Clients.Caller.SendAsync("EditMessageError", errorMessage);
        }
    }

    /// <summary>
    /// Клиент вызывает этот метод для удаления сообщения.
    /// </summary>
    public async Task DeleteMessage(Guid messageId)
    {
        var deleterId = GetCurrentUserId();

        var (success, errorMessage) = await _messageRepository.DeleteMessageAsync(messageId, deleterId);

        if (success)
        {
            
            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message != null)
            {
                await Clients.Group(message.ChatId.ToString()).SendAsync("MessageDeleted", messageId);
                // TODO: Публикация события "MessageDeleted" в брокер сообщений
            } else {
                await Clients.Caller.SendAsync("DeleteMessageError", "Сообщение не найдено после попытки удаления.");
            }
        }
        else
        {
            await Clients.Caller.SendAsync("DeleteMessageError", errorMessage);
        }
    }

    private async Task<bool> CheckUserIsParticipant(Guid chatId, Guid userId)
    {
        try
        {
            var response = await _chatHttpClient.GetAsync($"/api/chats/{chatId}");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatServiceChatResponse>(jsonResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return chatResponse?.ParticipantIds?.Contains(userId) ?? false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdString = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found or invalid in JWT token.");
        }

        return userId;
    }
}