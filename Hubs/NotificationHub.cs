using ClassificadorDoc.Services.RealTime;
using ClassificadorDoc.Models;
using ClassificadorDoc.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ClassificadorDoc.Hubs
{
    /// <summary>
    /// Hub SignalR expandido para notificações e usuários conectados em tempo real
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        private readonly IConnectedUsersService _connectedUsersService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationHub(
            ILogger<NotificationHub> logger,
            IConnectedUsersService connectedUsersService,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _connectedUsersService = connectedUsersService;
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.UserIdentifier;
                var user = await _userManager.FindByIdAsync(userId ?? "");
                var userName = user?.UserName ?? "Usuário Anônimo";

                _logger.LogInformation("Cliente conectado ao hub: {ConnectionId} - Usuário: {UserName} ({UserId})",
                    Context.ConnectionId, userName, userId);

                // Adicionar usuário à lista de conectados
                if (!string.IsNullOrEmpty(userId))
                {
                    await _connectedUsersService.AddUserAsync(Context.ConnectionId, userId, userName);
                    // Adicionar à grupo específico do usuário para notificações direcionadas
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao conectar usuário: {ConnectionId}", Context.ConnectionId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = Context.UserIdentifier;
                var connectionId = Context.ConnectionId;

                _logger.LogInformation("Cliente desconectado do hub: {ConnectionId} - Usuário: {UserId}",
                    connectionId, userId);

                // Remover usuário da lista de conectados
                await _connectedUsersService.RemoveUserAsync(connectionId);

                // Remover do grupo do usuário
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.RemoveFromGroupAsync(connectionId, $"User_{userId}");
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao desconectar usuário: {ConnectionId}", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Método chamado pelo cliente para indicar atividade
        /// </summary>
        public async Task UpdateActivity()
        {
            try
            {
                await _connectedUsersService.UpdateLastActivityAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar atividade do usuário: {ConnectionId}", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Método para o cliente solicitar lista de usuários conectados
        /// </summary>
        public async Task RequestConnectedUsers()
        {
            try
            {
                await _connectedUsersService.BroadcastConnectedUsersUpdateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao solicitar usuários conectados: {ConnectionId}", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Entrar em grupo específico (mantido para compatibilidade)
        /// </summary>
        public async Task JoinUserGroup(string userId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogDebug("Usuário {UserId} adicionado ao grupo User_{UserId}", userId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar usuário ao grupo: {UserId}", userId);
            }
        }

        /// <summary>
        /// Sair de grupo específico (mantido para compatibilidade)
        /// </summary>
        public async Task LeaveUserGroup(string userId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogDebug("Usuário {UserId} removido do grupo User_{UserId}", userId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover usuário do grupo: {UserId}", userId);
            }
        }

        /// <summary>
        /// Ping para manter conexão ativa
        /// </summary>
        public async Task Ping()
        {
            try
            {
                await _connectedUsersService.UpdateLastActivityAsync(Context.ConnectionId);
                await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ping: {ConnectionId}", Context.ConnectionId);
            }
        }
    }
}
