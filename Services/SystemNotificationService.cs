using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Interface para gerenciamento de notificações do sistema
    /// </summary>
    public interface ISystemNotificationService
    {
        Task CreateNotificationAsync(string title, string message, string type = "INFO",
            string priority = "NORMAL", string? userId = null, int? alertId = null,
            bool playSound = true, bool showToast = true, string? actionUrl = null);

        Task<List<SystemNotification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false);
        Task<List<SystemNotification>> GetActiveNotificationsAsync();
        Task MarkAsReadAsync(int notificationId, string userId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteExpiredNotificationsAsync();
        Task BroadcastNotificationAsync(SystemNotification notification);
    }

    /// <summary>
    /// Serviço para gerenciar notificações in-app, toasts e sons
    /// </summary>
    public class SystemNotificationService : ISystemNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SystemNotificationService> _logger;
        private readonly IHubContext<NotificationHub>? _hubContext;

        public SystemNotificationService(
            ApplicationDbContext context,
            ILogger<SystemNotificationService> logger,
            IHubContext<NotificationHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task CreateNotificationAsync(string title, string message, string type = "INFO",
            string priority = "NORMAL", string? userId = null, int? alertId = null,
            bool playSound = true, bool showToast = true, string? actionUrl = null)
        {
            try
            {
                var notification = new SystemNotification
                {
                    Title = title,
                    Message = message,
                    Type = type.ToUpper(),
                    Priority = priority.ToUpper(),
                    UserId = userId,
                    AlertId = alertId,
                    PlaySound = playSound && (priority == "HIGH" || priority == "URGENT"),
                    ShowToast = showToast,
                    ActionUrl = actionUrl,
                    ExpiresAt = DateTime.UtcNow.AddDays(7), // Expirar em 7 dias
                    Icon = GetIconByType(type),
                    Color = GetColorByType(type)
                };

                _context.SystemNotifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Notificação criada: {Title} para usuário {UserId}", title, userId ?? "TODOS");

                // Enviar via SignalR se disponível
                await BroadcastNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar notificação: {Title}", title);
            }
        }

        public async Task<List<SystemNotification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
        {
            var query = _context.SystemNotifications
                .Where(n => n.UserId == userId || n.UserId == null)
                .Where(n => !n.ExpiresAt.HasValue || n.ExpiresAt > DateTime.UtcNow);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task<List<SystemNotification>> GetActiveNotificationsAsync()
        {
            return await _context.SystemNotifications
                .Where(n => !n.IsDisplayed && n.ShowToast)
                .Where(n => !n.ExpiresAt.HasValue || n.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(n => n.Priority == "URGENT" ? 4 : n.Priority == "HIGH" ? 3 : n.Priority == "NORMAL" ? 2 : 1)
                .ThenByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.SystemNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId &&
                    (n.UserId == userId || n.UserId == null));

            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var notifications = await _context.SystemNotifications
                .Where(n => !n.IsRead && (n.UserId == userId || n.UserId == null))
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteExpiredNotificationsAsync()
        {
            var expiredNotifications = await _context.SystemNotifications
                .Where(n => n.ExpiresAt.HasValue && n.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _context.SystemNotifications.RemoveRange(expiredNotifications);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Removidas {Count} notificações expiradas", expiredNotifications.Count);
        }

        public async Task BroadcastNotificationAsync(SystemNotification notification)
        {
            if (_hubContext == null) return;

            try
            {
                var notificationData = new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    type = notification.Type.ToLower(),
                    priority = notification.Priority.ToLower(),
                    playSound = notification.PlaySound,
                    showToast = notification.ShowToast,
                    icon = notification.Icon,
                    color = notification.Color,
                    actionUrl = notification.ActionUrl,
                    createdAt = notification.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                if (!string.IsNullOrEmpty(notification.UserId))
                {
                    // Enviar para usuário específico
                    await _hubContext.Clients.User(notification.UserId)
                        .SendAsync("ReceiveNotification", notificationData);
                }
                else
                {
                    // Broadcast para todos os usuários conectados
                    await _hubContext.Clients.All
                        .SendAsync("ReceiveNotification", notificationData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação via SignalR");
            }
        }

        private string GetIconByType(string type)
        {
            return type.ToUpper() switch
            {
                "ERROR" => "fas fa-exclamation-triangle",
                "WARNING" => "fas fa-exclamation-circle",
                "SUCCESS" => "fas fa-check-circle",
                "ALERT" => "fas fa-bell",
                "INFO" => "fas fa-info-circle",
                _ => "fas fa-info-circle"
            };
        }

        private string GetColorByType(string type)
        {
            return type.ToUpper() switch
            {
                "ERROR" => "danger",
                "WARNING" => "warning",
                "SUCCESS" => "success",
                "ALERT" => "primary",
                "INFO" => "info",
                _ => "primary"
            };
        }
    }

    /// <summary>
    /// Hub SignalR para notificações em tempo real
    /// </summary>
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Cliente conectado ao hub de notificações: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Cliente desconectado do hub de notificações: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task LeaveUserGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }
    }
}
