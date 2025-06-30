using System.Text.Json;
using MessageService.Constants;
using MessageService.DTOs;
using MessageService.Models;
using MessageService.Services;
using Microsoft.AspNetCore.SignalR;

namespace MessageService.Hubs;

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
    public async Task SendMessage(Guid chatId, Guid senderId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Message content cannot be empty.");
            return;
        }

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

        try
        {
            var savedMessage = await _messageRepository.CreateMessageAsync(message);

            await Clients.Group(chatId.ToString()).SendAsync("ReceiveMessage", new MessageResponse
            {
                Id = savedMessage.Id,
                ChatId = savedMessage.ChatId,
                SenderId = savedMessage.SenderId,
                Content = savedMessage.Content,
                Timestamp = savedMessage.Timestamp,
                IsEdited = savedMessage.IsEdited,
                IsDeleted = savedMessage.IsDeleted
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveError", $"Error sending message: {ex.Message}");
        }
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = await _presenceService.GetUserIdByConnectionId(Context.ConnectionId);

        await _presenceService.UserDisconnected(Context.ConnectionId);

        if (userId.HasValue)
        {
            var remainingConnections = await _presenceService.GetUserConnectionIds(userId.Value);
            if (remainingConnections.Count == 0)
            {
                await Clients.All.SendAsync("UserStatusChanged", userId.Value, false);
            }
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
    public async Task EditMessage(Guid chatId, Guid messageId, string newContent, Guid editorId)
    {
        if (string.IsNullOrWhiteSpace(newContent))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Message content cannot be empty.");
            return;
        }

        var requestDto = new EditMessageRequest
        {
            MessageId = messageId,
            NewContent = newContent,
            EditorId = editorId
        };

        var (success, errorMessage, updatedMessage) = await _messageRepository.EditMessageAsync(requestDto);

        if (success && updatedMessage != null)
        {
            await Clients.Group(chatId.ToString()).SendAsync("MessageEdited",
                updatedMessage.Id, updatedMessage.Content, updatedMessage.Timestamp, updatedMessage.IsEdited);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveError", $"Failed to edit message: {errorMessage}");
        }
    }
    
    /// <summary>
    /// Клиент вызывает этот метод для удаления сообщения.
    /// </summary>
    public async Task DeleteMessage(Guid chatId, Guid messageId, Guid deleterId)
    {
        if (deleterId == Guid.Empty)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Deleter ID is required.");
            return;
        }

        var chatExists = await CheckChatExists(chatId);
        if (!chatExists)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Chat not found.");
            return;
        }

        var isParticipant = await CheckUserIsParticipant(chatId, deleterId);
        if (!isParticipant)
        {
            await Clients.Caller.SendAsync("ReceiveError", "You are not a participant of this chat.");
            return;
        }

        var (success, errorMessage) = await _messageRepository.DeleteMessageAsync(messageId, deleterId);

        if (success)
        {
            await Clients.Group(chatId.ToString()).SendAsync("MessageDeleted", messageId, "[Сообщение удалено]", DateTimeOffset.UtcNow, true);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveError", $"Failed to delete message: {errorMessage}");
        }
    }

    private async Task<bool> CheckChatExists(Guid chatId)
    {
        var response = await _chatHttpClient.GetAsync($"/api/chats/{chatId}");
        return response.IsSuccessStatusCode;
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
}