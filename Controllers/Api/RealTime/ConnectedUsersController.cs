using ClassificadorDoc.Models.RealTime;
using ClassificadorDoc.Services.RealTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorDoc.Controllers.Api.RealTime
{
    /// <summary>
    /// API Controller para gerenciar usuários conectados em tempo real
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectedUsersController : ControllerBase
    {
        private readonly IConnectedUsersService _connectedUsersService;
        private readonly ILogger<ConnectedUsersController> _logger;

        public ConnectedUsersController(
            IConnectedUsersService connectedUsersService,
            ILogger<ConnectedUsersController> logger)
        {
            _connectedUsersService = connectedUsersService;
            _logger = logger;
        }

        /// <summary>
        /// Obtém a lista de todos os usuários conectados
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ConnectedUser>>> GetConnectedUsers()
        {
            try
            {
                var users = await _connectedUsersService.GetConnectedUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter usuários conectados");
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obtém estatísticas dos usuários conectados
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ConnectedUsersStats>> GetConnectedUsersStats()
        {
            try
            {
                var users = await _connectedUsersService.GetConnectedUsersAsync();
                var stats = new ConnectedUsersStats
                {
                    TotalConnected = users.Count,
                    ActiveUsers = users.Count(u => u.ActivityStatus == "Ativo"),
                    InactiveUsers = users.Count(u => u.ActivityStatus == "Inativo"),
                    AbsentUsers = users.Count(u => u.ActivityStatus == "Ausente"),
                    Users = users,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter estatísticas de usuários conectados");
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obtém apenas a contagem de usuários conectados
        /// </summary>
        [HttpGet("count")]
        public async Task<ActionResult<int>> GetConnectedUsersCount()
        {
            try
            {
                var count = await _connectedUsersService.GetConnectedUsersCountAsync();
                return Ok(new { count = count, timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter contagem de usuários conectados");
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Verifica se um usuário específico está conectado
        /// </summary>
        [HttpGet("check/{userId}")]
        public async Task<ActionResult<bool>> IsUserConnected(string userId)
        {
            try
            {
                var isConnected = await _connectedUsersService.IsUserConnectedAsync(userId);
                return Ok(new { userId = userId, isConnected = isConnected, timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar se usuário está conectado: {UserId}", userId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Remove usuários inativos manualmente (Admin only)
        /// </summary>
        [HttpPost("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> CleanupInactiveUsers([FromQuery] int inactiveMinutes = 30)
        {
            try
            {
                await _connectedUsersService.RemoveInactiveUsersAsync(inactiveMinutes);
                return Ok(new { message = "Limpeza de usuários inativos concluída", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar usuários inativos");
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Força atualização da lista de usuários conectados
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult> RefreshConnectedUsers()
        {
            try
            {
                await _connectedUsersService.BroadcastConnectedUsersUpdateAsync();
                return Ok(new { message = "Lista de usuários conectados atualizada", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar lista de usuários conectados");
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }
    }
}
