using System.Security.Claims;
using MessageService.DTOs;
using MessageService.Models;
using MessageService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessageService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageRepository _messageRepository;

    public MessagesController(IMessageRepository messageService)
    {
        _messageRepository = messageService ?? throw new ArgumentNullException(nameof(messageService));
    }

    /// <summary>
    /// Получает историю сообщений для конкретного чата.
    /// </summary>
    [HttpGet("chat/{chatId:guid}")]
    public async Task<ActionResult<IEnumerable<MessageResponse>>> GetChatMessages(Guid chatId)
    {
        var messages = await _messageRepository.GetMessagesByChatIdAsync(chatId);

        var responseMessages = messages.Select(ToMessageResponse).ToList();
        return Ok(responseMessages);
    }

    /// <summary>
    /// Редактирует существующее сообщение.
    /// </summary>
    [HttpPut("edit")]
    public async Task<ActionResult> EditMessage([FromBody] EditMessageRequest request)
    {
        var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
        {
            return Unauthorized("Не удалось определить ID пользователя из токена.");
        }
        
        request.EditorId = currentUserId;

        var (success, errorMessage, updatedMessage) = await _messageRepository.EditMessageAsync(request);

        if (success) return NoContent();

        if (errorMessage.Contains("not found"))
        {
            return NotFound(errorMessage);
        }

        if (errorMessage.Contains("Only the sender"))
        {
            return Forbid(errorMessage);
        }

        return BadRequest(errorMessage);
    }
    
    /// <summary>
    /// Удаляет (помечает как удаленное) существующее сообщение.
    /// </summary>
    [HttpDelete("{messageId:guid}")]
    public async Task<ActionResult> DeleteMessage(Guid messageId)
    {
        var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
        {
            return Unauthorized("Не удалось определить ID пользователя из токена.");
        }
        
        var (success, errorMessage) = await _messageRepository.DeleteMessageAsync(messageId, currentUserId);

        if (success)
        {
            return NoContent();
        }

        if (errorMessage.Contains("not found"))
        {
            return NotFound(errorMessage);
        }

        if (errorMessage.Contains("Only the sender"))
        {
            return Forbid(errorMessage);
        }

        return BadRequest(errorMessage);
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