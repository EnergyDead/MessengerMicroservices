using MessageService.Constants;
using MessageService.Data;
using MessageService.DTOs;
using MessageService.Models;
using MessageService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MessageService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _chatHttpClient;
    private readonly IUserPresenceService _presenceService;

    public MessagesController(AppDbContext db,
        IUserPresenceService presenceService,
        IHttpClientFactory httpClientFactory)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _presenceService = presenceService ?? throw new ArgumentNullException(nameof(presenceService));
        _chatHttpClient = httpClientFactory.CreateClient(ServiceConstants.ChatServiceHttpClientName);
    }

    /// <summary>
    /// Получает историю сообщений для конкретного чата.
    /// </summary>
    [HttpGet("chat/{chatId:guid}")]
    public async Task<ActionResult<IEnumerable<MessageResponse>>> GetChatMessages(Guid chatId)
    {
        // todo добавить проверку что пользователь находится в чате

        var chatExists = await CheckChatExists(chatId);
        if (!chatExists)
        {
            return NotFound("Chat not found.");
        }

        var messages = await _db.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.Timestamp) // Сортируем по времени, чтобы получить в хронологическом порядке
            .Select(m => ToMessageResponse(m)) // Преобразуем в DTO
            .ToListAsync();

        return Ok(messages);
    }

    /// <summary>
    /// Возвращает сообщения, отправленные после указанной временной метки.
    /// Эндпоинт для NotificationService.
    /// </summary>
    [HttpGet("messages/since/{timestampMs:long}")]
    public async Task<ActionResult<IEnumerable<Message>>> GetMessagesSince(long timestampMs)
    {
        var dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        var messages = await _db.Messages
            .Where(m => m.Timestamp > dateTimeOffset)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        return Ok(messages);
    }

    /// <summary>
    /// Проверяет, находится ли пользователь онлайн.
    /// Эндпоинт для NotificationService.
    /// </summary>
    [HttpGet("users/online/{userId:guid}")]
    public async Task<ActionResult<bool>> IsUserOnline(Guid userId)
    {
        var isOnline = await _presenceService.IsUserOnline(userId);
        return Ok(isOnline);
    }

    private async Task<bool> CheckChatExists(Guid chatId)
    {
        var response = await _chatHttpClient.GetAsync($"{ServiceConstants.ChatServiceBaseApiPath}/{chatId}");
        return response.IsSuccessStatusCode;
    }

    private static MessageResponse ToMessageResponse(Message message)
    {
        return new MessageResponse
        {
            Id = message.Id,
            ChatId = message.ChatId,
            SenderId = message.SenderId,
            Content = message.Content,
            Timestamp = message.Timestamp
        };
    }
}