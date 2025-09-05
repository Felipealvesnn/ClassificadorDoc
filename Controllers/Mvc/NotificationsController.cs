using ClassificadorDoc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize(Roles = "Admin")]
    public class NotificationsController : Controller
    {
        private readonly ISystemNotificationService _notificationService;

        public NotificationsController(ISystemNotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPost]
        public async Task<IActionResult> TestAlert()
        {
            await _notificationService.CreateNotificationAsync(
                title: "🚨 Teste de Alerta do Sistema",
                message: "Este é um teste do sistema de alertas automáticos. Se você está vendo isso, o sistema está funcionando corretamente!",
                type: "ALERT",
                priority: "HIGH",
                userId: null, // Para todos os administradores
                alertId: null,
                playSound: true,
                showToast: true,
                actionUrl: "/Alertas"
            );

            TempData["SuccessMessage"] = "Alerta de teste enviado com sucesso!";
            return RedirectToAction("Index", "Alertas");
        }

        [HttpGet]
        public IActionResult TestPage()
        {
            return View();
        }
    }
}
