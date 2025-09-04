using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using ApplicationUser = ClassificadorDoc.Data.ApplicationUser;
using ApplicationRole = ClassificadorDoc.Data.ApplicationRole;

namespace ClassificadorDoc.Controllers.Mvc
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Prevenir login se já autenticado
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["AllowRegistration"] = "false"; // Para empresa, geralmente não permite auto-registro

            // Adicionar headers de segurança
            Response.Headers["X-Frame-Options"] = "DENY";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["AllowRegistration"] = "false";

            // Rate limiting básico - em produção usar middleware específico
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (ModelState.IsValid)
            {
                // Normalizar email para busca
                var email = model.Email.Trim().ToLowerInvariant();
                var user = await _userManager.FindByEmailAsync(email);

                if (user != null && user.IsActive)
                {
                    var result = await _signInManager.PasswordSignInAsync(
                        user.UserName!,
                        model.Password,
                        model.RememberMe,
                        lockoutOnFailure: true);

                    if (result.Succeeded)
                    {
                        // Atualizar último login
                        user.LastLoginAt = DateTime.UtcNow;
                        await _userManager.UpdateAsync(user);

                        _logger.LogInformation("Usuário {Email} fez login com sucesso", user.Email);

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        return RedirectToAction("Index", "Home");
                    }

                    if (result.IsLockedOut)
                    {
                        ModelState.AddModelError(string.Empty, "Conta bloqueada temporariamente devido a muitas tentativas de login inválidas.");
                        _logger.LogWarning("Tentativa de login em conta bloqueada: {Email}", model.Email);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
                        _logger.LogWarning("Tentativa de login inválida para: {Email}", model.Email);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
                    _logger.LogWarning("Tentativa de login para usuário inexistente ou inativo: {Email}", model.Email);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    Department = model.Department ?? "Não informado"
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Adicionar role padrão de User
                    await _userManager.AddToRoleAsync(user, "User");

                    _logger.LogInformation("Novo usuário criado: {Email}", user.Email);

                    // Auto login após registro
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, GetPortugueseErrorMessage(error.Code));
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Usuário fez logout");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserProfileViewModel
            {
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                Department = user.Department ?? "",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                DocumentsProcessed = user.DocumentsProcessed,
                LastDocumentProcessedAt = user.LastDocumentProcessedAt,
                Roles = roles.ToList()
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UserManagement()
        {
            var users = _userManager.Users.OrderBy(u => u.FullName).ToList();
            var userViewModels = new List<UserManagementViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserManagementViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName ?? "",
                    Email = user.Email ?? "",
                    Department = user.Department ?? "",
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    DocumentsProcessed = user.DocumentsProcessed,
                    Roles = roles.ToList()
                });
            }

            return View(userViewModels);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("Status do usuário {Email} alterado para {Status}", user.Email, user.IsActive ? "Ativo" : "Inativo");
            }

            return RedirectToAction("UserManagement");
        }

        private string GetPortugueseErrorMessage(string errorCode)
        {
            return errorCode switch
            {
                "DuplicateEmail" => "Este email já está sendo usado por outro usuário.",
                "PasswordTooShort" => "A senha deve ter pelo menos 6 caracteres.",
                "PasswordRequiresDigit" => "A senha deve conter pelo menos um número.",
                "PasswordRequiresLower" => "A senha deve conter pelo menos uma letra minúscula.",
                "PasswordRequiresUpper" => "A senha deve conter pelo menos uma letra maiúscula.",
                "PasswordRequiresNonAlphanumeric" => "A senha deve conter pelo menos um caractere especial.",
                "InvalidEmail" => "Email inválido.",
                _ => "Erro desconhecido."
            };
        }
    }
}
