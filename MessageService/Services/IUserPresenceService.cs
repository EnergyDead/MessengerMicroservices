namespace MessageService.Services;

public interface IUserPresenceService
{
    Task UserConnected(Guid userId, string connectionId);
    Task UserDisconnected(string connectionId);
    Task<bool> IsUserOnline(Guid userId);
    Task<List<string>> GetUserConnectionIds(Guid userId);
    Task<Guid?> GetUserIdByConnectionId(string connectionId);
}