using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Имя пользователя обязательно.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Имя пользователя должно быть от 3 до 100 символов.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email обязателен.")]
    [EmailAddress(ErrorMessage = "Некорректный формат Email.")]
    [StringLength(100, ErrorMessage = "Email не должен превышать 100 символов.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен.")]
    [StringLength(50, MinimumLength = 6, ErrorMessage = "Пароль должен быть от 6 до 50 символов.")]
    public string Password { get; set; } = string.Empty;
}