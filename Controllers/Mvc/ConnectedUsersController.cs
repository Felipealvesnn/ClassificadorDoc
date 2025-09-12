using ClassificadorDoc.Services.RealTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorDoc.Controllers.Mvc
{
    /// <summary>
    /// Controller MVC para visualização de usuários conectados
    /// </summary>
    [Authorize]
    public class ConnectedUsersController : Controller
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
        /// Página principal de visualização de usuários conectados
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar página de usuários conectados");
                TempData["ErrorMessage"] = "Erro ao carregar página de usuários conectados";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
