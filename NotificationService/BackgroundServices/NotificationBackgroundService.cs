using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.BackgroundServices;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _emailDelay;
    private DateTimeOffset _lastMessageCheckTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

    public NotificationBackgroundService(IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _pollingInterval =
            TimeSpan.FromSeconds(configuration.GetValue<int>("NotificationSettings:PollingIntervalSeconds"));
        _emailDelay = TimeSpan.FromMinutes(configuration.GetValue<int>("NotificationSettings:EmailDelayMinutes"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWork(stoppingToken);
            }
            catch (Exception)
            {
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var messagePollingService = scope.ServiceProvider.GetRequiredService<IMessagePollingService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var newMessages = await messagePollingService.GetUnprocessedMessagesAsync(_lastMessageCheckTimestamp);

            if (newMessages.Count != 0)
            {
                _lastMessageCheckTimestamp = newMessages.Max(m => m.Timestamp);
            }

            foreach (var newMessage in newMessages)
            {
                var participants = await messagePollingService.GetChatParticipantsAsync(newMessage.ChatId);

                foreach (var participantId in participants)
                {
                    if (participantId == newMessage.SenderId) continue;

                    var exists = await dbContext.MessageNotifications
                        .AnyAsync(mn => mn.MessageId == newMessage.Id && mn.RecipientId == participantId);

                    if (!exists)
                    {
                        var notification = new MessageNotification
                        {
                            Id = Guid.NewGuid(),
                            MessageId = newMessage.Id,
                            ChatId = newMessage.ChatId,
                            SenderId = newMessage.SenderId,
                            RecipientId = participantId,
                            SentTimestamp = newMessage.Timestamp,
                            IsRead = false,
                            IsEmailSent = false
                        };
                        await dbContext.MessageNotifications.AddAsync(notification);
                    }
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            var unreadNotifications = await dbContext.MessageNotifications
                .Where(mn => !mn.IsRead && !mn.IsEmailSent &&
                             mn.SentTimestamp.Add(_emailDelay) <= DateTimeOffset.UtcNow)
                .ToListAsync(stoppingToken);

            foreach (var notification in unreadNotifications)
            {
                var isUserOnline = await messagePollingService.IsUserOnlineAsync(notification.RecipientId);

                if (isUserOnline) continue;

                var subject = "Новое сообщение в чате!";
                var body =
                    $"Вам пришло новое сообщение в чате '{notification.ChatId.ToString().Substring(0, 8)}'. Пожалуйста, войдите в мессенджер, чтобы прочитать его.";
                var originalMessage = newMessages.FirstOrDefault(m => m.Id == notification.MessageId);

                if (originalMessage != null)
                {
                    body =
                        $"Вам пришло новое сообщение от пользователя {notification.SenderId.ToString().Substring(0, 8)} в чате '{notification.ChatId.ToString().Substring(0, 8)}': \"{originalMessage.Content}\". Пожалуйста, войдите в мессенджер, чтобы прочитать его.";
                }

                await emailService.SendEmailAsync(notification.RecipientId, subject, body);

                notification.IsEmailSent = true;
                notification.EmailSentTimestamp = DateTimeOffset.UtcNow;
                dbContext.MessageNotifications.Update(notification);
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}