namespace NotificationService.Services;

public class DummyEmailService : IEmailService
{
    public Task SendEmailAsync(Guid recipientId, string subject, string body)
    {
        // todo Заглушка
        
        return Task.CompletedTask;
    }
}