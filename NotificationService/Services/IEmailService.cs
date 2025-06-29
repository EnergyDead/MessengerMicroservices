namespace NotificationService.Services;

public interface IEmailService
{
    Task SendEmailAsync(Guid recipientId, string subject, string body);
}