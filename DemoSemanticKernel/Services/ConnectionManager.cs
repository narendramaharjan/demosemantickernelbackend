using System.Collections.Concurrent;

namespace DemoSemanticKernel.Services
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly ConcurrentDictionary<string, string> _connections = new();
        private string? _currentConnectionId;

        public string? GetCurrentConnection()
        {
            if (_currentConnectionId != null && _connections.TryGetValue(_currentConnectionId, out var connection))
            {
                return connection;
            }
            return null;
        }

        public void SetConnection(string connectionString)
        {
            var connectionId = Guid.NewGuid().ToString();
            _connections[connectionId] = connectionString;
            _currentConnectionId = connectionId;
        }

        public bool IsConnected()
        {
            return !string.IsNullOrEmpty(GetCurrentConnection());
        }
    }
}
