using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Data;

namespace ClassificadorDoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(IServiceProvider serviceProvider, ILogger<DatabaseController> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Verifica o status atual do banco de dados
        /// </summary>
        /// <returns>Status do banco de dados</returns>
        [HttpGet("status")]
        public async Task<ActionResult<DatabaseStatus>> GetDatabaseStatus()
        {
            try
            {
                var status = await DatabaseInitializer.GetDatabaseStatusAsync(_serviceProvider);

                if (status.IsHealthy)
                {
                    return Ok(status);
                }
                else
                {
                    return StatusCode(503, status); // Service Unavailable
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar status do banco de dados");
                return StatusCode(500, new { error = "Erro ao verificar status do banco de dados", message = ex.Message });
            }
        }

        /// <summary>
        /// Força a reinicialização do banco de dados (apenas em desenvolvimento)
        /// </summary>
        /// <returns>Resultado da operação</returns>
        [HttpPost("reinitialize")]
        public async Task<IActionResult> ReinitializeDatabase()
        {
            // Só permite em ambiente de desenvolvimento
            if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                return Forbid("Esta operação só é permitida em ambiente de desenvolvimento");
            }

            try
            {
                var app = HttpContext.RequestServices.GetRequiredService<WebApplication>();
                await DatabaseInitializer.InitializeAsync(app);

                return Ok(new { message = "Banco de dados reinicializado com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao reinicializar banco de dados");
                return StatusCode(500, new { error = "Erro ao reinicializar banco de dados", message = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint para health check simples
        /// </summary>
        /// <returns>Status de saúde do banco</returns>
        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var status = await DatabaseInitializer.GetDatabaseStatusAsync(_serviceProvider);

                if (status.CanConnect && string.IsNullOrEmpty(status.Error))
                {
                    return Ok(new { status = "healthy", canConnect = true });
                }
                else
                {
                    return StatusCode(503, new { status = "unhealthy", canConnect = false, error = status.Error });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { status = "unhealthy", error = ex.Message });
            }
        }
    }
}
