using ClassificadorDoc.Models.RealTime;
using ClassificadorDoc.Services.RealTime;
using ClassificadorDoc.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace ClassificadorDoc.Services.RealTime
{
    /// <summary>
    /// Serviço para gerenciar usuários conectados em tempo real usando SignalR
    /// </summary>
    public class ConnectedUsersService : IConnectedUsersService
    {
        private readonly ILogger<ConnectedUsersService> _logger;
        private readonly IHubContext<NotificationHub>? _hubContext;

        // Dicionário thread-safe para armazenar usuários conectados em memória
        private static readonly ConcurrentDictionary<string, ConnectedUser> _connectedUsers = new();

        public ConnectedUsersService(
            ILogger<ConnectedUsersService> logger,
            IHubContext<NotificationHub>? hubContext = null)
        {
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task AddUserAsync(string connectionId, string userId, string userName)
        {
            try
            {
                // Primeiro, remover todas as conexões antigas do mesmo usuário
                var oldConnections = _connectedUsers
                    .Where(kvp => kvp.Value.UserId == userId && kvp.Key != connectionId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var oldConnectionId in oldConnections)
                {
                    if (_connectedUsers.TryRemove(oldConnectionId, out var removedUser))
                    {
                        _logger.LogInformation("Conexão antiga removida para usuário {UserName}: {OldConnectionId}",
                            removedUser.UserName, oldConnectionId);
                    }
                }

                var connectedUser = new ConnectedUser
                {
                    ConnectionId = connectionId,
                    UserId = userId,
                    UserName = userName,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IsActive = true
                };

                _connectedUsers.AddOrUpdate(connectionId, connectedUser, (key, existingUser) =>
                {
                    existingUser.LastActivity = DateTime.UtcNow;
                    existingUser.IsActive = true;
                    return existingUser;
                });

                _logger.LogInformation("Usuário {UserName} ({UserId}) conectado via {ConnectionId}",
                    userName, userId, connectionId);

                // Notificar todos os clientes sobre a atualização
                await BroadcastConnectedUsersUpdateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar usuário conectado: {UserId}", userId);
            }
        }

        public async Task RemoveUserAsync(string connectionId)
        {
            try
            {
                if (_connectedUsers.TryRemove(connectionId, out var removedUser))
                {
                    _logger.LogInformation("Usuário {UserName} ({UserId}) desconectado via {ConnectionId}",
                        removedUser.UserName, removedUser.UserId, connectionId);

                    // Notificar todos os clientes sobre a atualização
                    await BroadcastConnectedUsersUpdateAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover usuário conectado: {ConnectionId}", connectionId);
            }
        }

        public async Task<List<ConnectedUser>> GetConnectedUsersAsync()
        {
            try
            {
                // Remover usuários inativos antes de retornar a lista
                await RemoveInactiveUsersAsync();

                // Agrupar por UserId e pegar apenas a conexão mais recente de cada usuário
                return _connectedUsers.Values
                    .GroupBy(u => u.UserId)
                    .Select(g => g.OrderByDescending(u => u.LastActivity).First())
                    .OrderBy(u => u.UserName)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter usuários conectados");
                return new List<ConnectedUser>();
            }
        }

        public async Task<int> GetConnectedUsersCountAsync()
        {
            try
            {
                // Remover usuários inativos antes de contar
                await RemoveInactiveUsersAsync();

                return _connectedUsers.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter contagem de usuários conectados");
                return 0;
            }
        }

        public async Task<bool> IsUserConnectedAsync(string userId)
        {
            try
            {
                return await Task.FromResult(_connectedUsers.Values.Any(u => u.UserId == userId && u.IsActive));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar se usuário está conectado: {UserId}", userId);
                return false;
            }
        }

        public async Task UpdateLastActivityAsync(string connectionId)
        {
            try
            {
                if (_connectedUsers.TryGetValue(connectionId, out var user))
                {
                    user.LastActivity = DateTime.UtcNow;
                    user.IsActive = true;
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar última atividade: {ConnectionId}", connectionId);
            }
        }

        public async Task RemoveInactiveUsersAsync(int inactiveMinutes = 30)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-inactiveMinutes);
                var inactiveConnections = _connectedUsers
                    .Where(kvp => kvp.Value.LastActivity < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                var removedCount = 0;
                foreach (var connectionId in inactiveConnections)
                {
                    if (_connectedUsers.TryRemove(connectionId, out var removedUser))
                    {
                        removedCount++;
                        _logger.LogInformation("Usuário inativo removido: {UserName} ({ConnectionId})",
                            removedUser.UserName, connectionId);
                    }
                }

                if (removedCount > 0)
                {
                    _logger.LogInformation("Removidos {Count} usuários inativos", removedCount);
                    await BroadcastConnectedUsersUpdateAsync();
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover usuários inativos");
            }
        }

        public async Task BroadcastConnectedUsersUpdateAsync()
        {
            if (_hubContext == null) return;

            try
            {
                var users = await GetConnectedUsersAsync();
                var stats = new ConnectedUsersStats
                {
                    TotalConnected = users.Count,
                    ActiveUsers = users.Count(u => u.ActivityStatus == "Ativo"),
                    InactiveUsers = users.Count(u => u.ActivityStatus == "Inativo"),
                    AbsentUsers = users.Count(u => u.ActivityStatus == "Ausente"),
                    Users = users,
                    LastUpdated = DateTime.UtcNow
                };

                // Broadcast para todos os clientes conectados
                await _hubContext.Clients.All.SendAsync("ConnectedUsersUpdate", stats);

                _logger.LogDebug("Atualização de usuários conectados enviada: {Count} usuários", users.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atualização de usuários conectados via SignalR");
            }
        }
    }
}
