namespace MessageService.Services;

public interface IUserPresenceService
{
    Task UserConnected(Guid userId, string connectionId);
    Task UserDisconnected(Guid userId, string connectionId);
    Task<bool> IsUserOnline(Guid userId);
    Task<List<Guid>> GetOnlineUsers();
    Task<List<string>> GetUserConnectionIds(Guid userId);
}