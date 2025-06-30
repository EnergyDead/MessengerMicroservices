using StackExchange.Redis;

namespace MessageService.Services;

public class UserPresenceService : IUserPresenceService
{
    private readonly IDatabase _redisDb;
    private const string ConnectionIdToUserIdKeyPrefix = "conn:user:";
    private const string UserIdToConnectionsKeyPrefix = "user:conns:";
    private readonly TimeSpan _connectionExpiry = TimeSpan.FromHours(1);

    public UserPresenceService(IConnectionMultiplexer redis)
    {
        _redisDb = redis.GetDatabase();
    }

    public async Task UserConnected(Guid userId, string connectionId)
    {
        await _redisDb.StringSetAsync($"{ConnectionIdToUserIdKeyPrefix}{connectionId}", userId.ToString(),
            _connectionExpiry);

        await _redisDb.SetAddAsync($"{UserIdToConnectionsKeyPrefix}{userId}", connectionId);

        await _redisDb.KeyExpireAsync($"{UserIdToConnectionsKeyPrefix}{userId}", _connectionExpiry);
    }

    public async Task UserDisconnected(string connectionId)
    {
        var userIdString = await _redisDb.StringGetDeleteAsync($"{ConnectionIdToUserIdKeyPrefix}{connectionId}");
        if (userIdString.HasValue)
        {
            var userId = Guid.Parse(userIdString);

            await _redisDb.SetRemoveAsync($"{UserIdToConnectionsKeyPrefix}{userId}", connectionId);

            if (await _redisDb.SetLengthAsync($"{UserIdToConnectionsKeyPrefix}{userId}") == 0)
            {
                await _redisDb.KeyDeleteAsync($"{UserIdToConnectionsKeyPrefix}{userId}");
            }
        }
    }

    public async Task<bool> IsUserOnline(Guid userId)
    {
        return await _redisDb.KeyExistsAsync($"{UserIdToConnectionsKeyPrefix}{userId}") &&
               await _redisDb.SetLengthAsync($"{UserIdToConnectionsKeyPrefix}{userId}") > 0;
    }

    public async Task<List<string>> GetUserConnectionIds(Guid userId)
    {
        var connections = await _redisDb.SetMembersAsync($"{UserIdToConnectionsKeyPrefix}{userId}");

        return connections.Select(conn => conn.ToString()).ToList();
    }
    
    public async Task<Guid?> GetUserIdByConnectionId(string connectionId)
    {
        var userIdString = await _redisDb.StringGetAsync($"{ConnectionIdToUserIdKeyPrefix}{connectionId}");
        if (userIdString.HasValue && Guid.TryParse(userIdString, out var userId))
        {
            return userId;
        }
        return null;
    }
}