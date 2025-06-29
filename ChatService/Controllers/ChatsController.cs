using ChatService.Data;
using ChatService.DTOs;
using ChatService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;

    private const string UserServiceUrl = "http://localhost:5267"; // todo: вынести в настройки

    public ChatsController(AppDbContext db)
    {
        _db = db;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Создает новый личный чат между двумя пользователями.
    /// </summary>
    [HttpPost("personal")]
    public async Task<ActionResult<ChatResponse>> CreatePersonalChat(CreatePersonalChatRequest request)
    {
        var user1Exists = await CheckUserExists(request.User1Id);
        var user2Exists = await CheckUserExists(request.User2Id);
        if (!user1Exists || !user2Exists)
        {
            return BadRequest("One or both users do not exist.");
        }

        // Проверяем, существует ли уже личный чат между этими двумя пользователями
        var existingChat = await _db.Chats
            .Where(c => c.Type == ChatType.Personal)
            .Where(c => c.Participants.Any(p => p.UserId == request.User1Id) &&
                        c.Participants.Any(p => p.UserId == request.User2Id))
            .FirstOrDefaultAsync();

        if (existingChat != null)
        {
            return Conflict("Personal chat between these users already exists.");
        }

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Personal,
            Participants = new List<UserChat>
            {
                new UserChat { UserId = request.User1Id },
                new UserChat { UserId = request.User2Id }
            }
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, ToChatResponse(chat));
    }

    /// <summary>
    /// Создает новый групповой чат.
    /// </summary>
    [HttpPost("group")]
    public async Task<ActionResult<ChatResponse>> CreateGroupChat(CreateGroupChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Group chat must have a name.");
        }

        if (request.ParticipantIds.Count < 3)
        {
            return BadRequest("A group chat must have at least 3 participants.");
        }

        var distinctParticipantIds = request.ParticipantIds.Distinct().ToList();
        if (distinctParticipantIds.Count != request.ParticipantIds.Count)
        {
            return BadRequest("Participant list contains duplicate user IDs.");
        }

        foreach (var userId in request.ParticipantIds)
        {
            if (!await CheckUserExists(userId))
            {
                return BadRequest($"User with ID {userId} does not exist.");
            }
        }

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Group,
            Name = request.Name,
            Participants = request.ParticipantIds.Select(id => new UserChat { UserId = id }).ToList()
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, ToChatResponse(chat));
    }

    /// <summary>
    /// Получает информацию о чате по его ID.
    /// </summary>
    [HttpGet("{chatId:guid}")]
    public async Task<ActionResult<ChatResponse>> GetChat(Guid chatId)
    {
        var chat = await _db.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null)
        {
            return NotFound();
        }

        return Ok(ToChatResponse(chat));
    }

    /// <summary>
    /// Метод для проверки существования пользователя через UserService
    /// </summary>
    private async Task<bool> CheckUserExists(Guid userId)
    {
        // Отправляем GET-запрос к UserService
        var response = await _httpClient.GetAsync($"{UserServiceUrl}/api/users/{userId}");

        // Проверяем, успешен ли запрос (статус 2xx)
        return response.IsSuccessStatusCode;
    }

    private static ChatResponse ToChatResponse(Chat chat)
    {
        return new ChatResponse
        {
            Id = chat.Id,
            Type = chat.Type.ToString(),
            Name = chat.Name,
            ParticipantIds = chat.Participants.Select(p => p.UserId).ToList()
        };
    }
}