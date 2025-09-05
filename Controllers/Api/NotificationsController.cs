using ClassificadorDoc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassificadorDoc.Controllers.Api
{
    /// <summary>
    /// API Controller para gerenciar notificações do sistema
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly ISystemNotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            ISystemNotificationService notificationService,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Obter notificações do usuário atual
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly);

                var result = notifications.Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type.ToLower(),
                    priority = n.Priority.ToLower(),
                    isRead = n.IsRead,
                    playSound = n.PlaySound,
                    showToast = n.ShowToast,
                    icon = n.Icon,
                    color = n.Color,
                    actionUrl = n.ActionUrl,
                    createdAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    readAt = n.ReadAt?.ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter notificações do usuário");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obter notificações ativas para exibir como toast
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveNotifications()
        {
            try
            {
                var notifications = await _notificationService.GetActiveNotificationsAsync();

                var result = notifications.Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type.ToLower(),
                    priority = n.Priority.ToLower(),
                    playSound = n.PlaySound,
                    showToast = n.ShowToast,
                    icon = n.Icon,
                    color = n.Color,
                    actionUrl = n.ActionUrl,
                    createdAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter notificações ativas");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Marcar notificação como lida
        /// </summary>
        [HttpPost("{id}/mark-read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                await _notificationService.MarkAsReadAsync(id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao marcar notificação como lida");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Marcar todas as notificações como lidas
        /// </summary>
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                await _notificationService.MarkAllAsReadAsync(userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao marcar todas as notificações como lidas");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Criar uma notificação de teste (apenas para admins)
        /// </summary>
        [HttpPost("test")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CreateTestNotification([FromBody] TestNotificationRequest request)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    title: request.Title ?? "Notificação de Teste",
                    message: request.Message ?? "Esta é uma notificação de teste do sistema.",
                    type: request.Type ?? "INFO",
                    priority: request.Priority ?? "NORMAL",
                    playSound: request.PlaySound,
                    showToast: request.ShowToast
                );

                return Ok(new { message = "Notificação de teste criada com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar notificação de teste");
                return StatusCode(500, "Erro interno do servidor");
            }
        }
    }

    /// <summary>
    /// Modelo para requisição de notificação de teste
    /// </summary>
    public class TestNotificationRequest
    {
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public bool PlaySound { get; set; } = true;
        public bool ShowToast { get; set; } = true;
    }
}
