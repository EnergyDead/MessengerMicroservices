using System.ComponentModel.DataAnnotations;

namespace MessageService.DTOs;

public class EditMessageRequest
{
    [Required]
    public Guid MessageId { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string NewContent { get; set; } = string.Empty;

    [Required]
    public Guid EditorId { get; set; }
}