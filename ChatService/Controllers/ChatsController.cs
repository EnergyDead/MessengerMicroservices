using System.Security.Claims;
using ChatService.Constants;
using ChatService.Data;
using ChatService.DTOs;
using ChatService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _userHttpClient;

    public ChatsController(AppDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _userHttpClient = httpClientFactory.CreateClient(ServiceConstants.UserServiceHttpClientName);
    }

    /// <summary>
    /// Создает новый личный чат между двумя пользователями.
    /// </summary>
    [HttpPost("personal")]
    public async Task<ActionResult<ChatResponse>> CreatePersonalChat(CreatePersonalChatRequest request)
    {
        var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
        {
            return Unauthorized("Не удалось определить ID пользователя из токена.");
        }

        if (currentUserId != request.User1Id && currentUserId != request.User2Id)
        {
            return Forbid("Вы должны быть одним из участников личного чата для его создания.");
        }

        var existingChat = await _db.Chats
            .Where(c => c.Type == ChatType.Personal)
            .Where(c => c.Participants.Count == 2 &&
                        c.Participants.Any(p => p.UserId == request.User1Id) &&
                        c.Participants.Any(p => p.UserId == request.User2Id))
            .FirstOrDefaultAsync();

        if (existingChat != null)
        {
            return Conflict($"Личный чат между {request.User1Id} и {request.User2Id} уже существует.");
        }

        var newChat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Personal,
            Name = null
        };

        newChat.Participants.Add(new UserChat { UserId = request.User1Id });
        newChat.Participants.Add(new UserChat { UserId = request.User2Id });

        _db.Chats.Add(newChat);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetChat), new { chatId = newChat.Id }, ToChatResponse(newChat));
    }

    /// <summary>
    /// Создает новый групповой чат.
    /// </summary>
    [HttpPost("group")]
    public async Task<ActionResult<ChatResponse>> CreateGroupChat(CreateGroupChatRequest request)
    {
        var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
        {
            return Unauthorized("Не удалось определить ID пользователя из токена.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Имя группового чата обязательно.");
        }

        if (request.ParticipantIds.Count == 0)
        {
            return BadRequest("В групповом чате должны быть участники.");
        }

        if (!request.ParticipantIds.Contains(currentUserId))
        {
            return Forbid("Вы должны быть участником создаваемого группового чата.");
        }

        var newChat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Group,
            Name = request.Name
        };

        foreach (var userId in request.ParticipantIds.Distinct())
        {
            newChat.Participants.Add(new UserChat { UserId = userId });
        }

        _db.Chats.Add(newChat);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetChat), new { chatId = newChat.Id }, ToChatResponse(newChat));
    }

    /// <summary>
    /// Получает информацию о чате по его ID.
    /// </summary>
    [HttpGet("{chatId:guid}")]
    public async Task<ActionResult<ChatResponse>> GetChat(Guid chatId)
    {
        var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
        {
            return Unauthorized("Не удалось определить ID пользователя из токена.");
        }

        var chat = await _db.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null)
        {
            return NotFound("Чат не найден.");
        }

        if (chat.Participants.All(cp => cp.UserId != currentUserId))
        {
            return Forbid("У вас нет доступа к этому чату.");
        }

        var chatResponse = new ChatResponse
        {
            Id = chat.Id,
            Type = chat.Type,
            Name = chat.Name,
            ParticipantIds = chat.Participants.Select(p => p.UserId).ToList()
        };

        return Ok(chatResponse);
    }

    /// <summary>
    /// Получает все чаты, в которых участвует заданный пользователь.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatResponse>>> GetUserChats()
    {
        var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var userId))
        {
            return Unauthorized("Не удалось определить ID пользователя из токена.");
        }

        if (!await CheckUserExists(userId))
        {
            return NotFound($"User with ID {userId} does not exist.");
        }

        var chats = await _db.Chats
            .Include(c => c.Participants)
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .ToListAsync();

        if (chats.Count == 0)
        {
            return Ok(new List<ChatResponse>());
        }

        return Ok(chats.Select(ToChatResponse));
    }

    /// <summary>
    /// Метод для проверки существования пользователя через UserService
    /// </summary>
    private async Task<bool> CheckUserExists(Guid userId)
    {
        var response = await _userHttpClient.GetAsync($"{ServiceConstants.UserServiceBaseApiPath}/{userId}");

        return response.IsSuccessStatusCode;
    }

    private static ChatResponse ToChatResponse(Chat chat)
    {
        return new ChatResponse
        {
            Id = chat.Id,
            Type = chat.Type,
            Name = chat.Name,
            ParticipantIds = chat.Participants.Select(p => p.UserId).ToList()
        };
    }
}