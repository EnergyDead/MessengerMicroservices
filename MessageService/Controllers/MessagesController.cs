using System.Text.Json;
using MessageService.Data;
using MessageService.DTOs;
using MessageService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MessageService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private const string ChatServiceUrl = "http://localhost:5000"; // todo: вынести в настройки

    public MessagesController(AppDbContext db)
    {
        _db = db;
        _httpClient = new HttpClient();
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
            // Используем ChatServiceChatResponse из пространства имен MessageService.Hubs
            // или создадим такой же DTO в DTOs контроллера, если не хотим зависеть от Hubs
            var chatResponse = JsonSerializer.Deserialize<ChatServiceChatResponse>(jsonResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return chatResponse?.ParticipantIds?.Contains(userId) ?? false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking participant: {ex.Message}");
            return false;
        }
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