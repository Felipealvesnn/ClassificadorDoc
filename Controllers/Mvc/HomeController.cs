using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Models;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            ViewData["UserName"] = User.Identity?.Name ?? "Usuário";
            ViewData["IsAdmin"] = User.IsInRole("Administrator");

            return View();
        }

        public IActionResult Dashboard()
        {
            ViewBag.Username = User.Identity?.Name;
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
