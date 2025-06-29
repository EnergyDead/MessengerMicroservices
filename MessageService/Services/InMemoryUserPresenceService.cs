using System.Collections.Concurrent;

namespace MessageService.Services
{
    public class InMemoryUserPresenceService : IUserPresenceService
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentHashSet<string>> _userConnections = new();

        public Task UserConnected(Guid userId, string connectionId)
        {
            var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentHashSet<string>());
            connections.Add(connectionId);
            Console.WriteLine(
                $"[Presence] User {userId.ToString().Substring(0, 8)} connected. Total connections for user: {connections.Count}");
            return Task.CompletedTask;
        }

        public Task UserDisconnected(Guid userId, string connectionId)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.TryRemove(connectionId);
                Console.WriteLine(
                    $"[Presence] User {userId.ToString().Substring(0, 8)} disconnected. Remaining connections for user: {connections.Count}");

                if (connections.IsEmpty)
                {
                    // Если у пользователя нет больше активных соединений, удаляем его из словаря
                    _userConnections.TryRemove(userId, out _);
                    Console.WriteLine($"[Presence] User {userId.ToString().Substring(0, 8)} is now offline.");
                }
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsUserOnline(Guid userId)
        {
            return Task.FromResult(_userConnections.ContainsKey(userId) && !_userConnections[userId].IsEmpty);
        }

        public Task<List<Guid>> GetOnlineUsers()
        {
            return Task.FromResult(_userConnections.Keys.ToList());
        }

        public Task<List<string>> GetUserConnectionIds(Guid userId)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                return Task.FromResult(connections.ToList());
            }

            return Task.FromResult(new List<string>());
        }
    }

    public class ConcurrentHashSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();

        public bool Add(T item) => _dictionary.TryAdd(item, 0);
        public bool TryRemove(T item) => _dictionary.TryRemove(item, out _);
        public bool Contains(T item) => _dictionary.ContainsKey(item);
        public bool IsEmpty => _dictionary.IsEmpty;
        public int Count => _dictionary.Count;
        public List<T> ToList() => _dictionary.Keys.ToList();
    }
}