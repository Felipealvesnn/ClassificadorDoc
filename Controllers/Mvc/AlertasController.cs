using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize(Roles = "Admin")]
    public class AlertasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAlertConditionEngine _conditionEngine;
        private readonly IAlertExecutionService _alertService;
        private readonly ILogger<AlertasController> _logger;

        public AlertasController(
            ApplicationDbContext context,
            IAlertConditionEngine conditionEngine,
            IAlertExecutionService alertService,
            ILogger<AlertasController> logger)
        {
            _context = context;
            _conditionEngine = conditionEngine;
            _alertService = alertService;
            _logger = logger;
        }

        // GET: /Alertas
        public async Task<IActionResult> Index()
        {
            var alertas = await _context.AutomatedAlerts
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(alertas);
        }

        // GET: /Alertas/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var alerta = await _context.AutomatedAlerts
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alerta == null)
            {
                return NotFound();
            }

            return View(alerta);
        }

        // GET: /Alertas/Create
        public IActionResult Create()
        {
            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables();
            return View();
        }

        // POST: /Alertas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAlertViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Validar condição
                    var validationResult = _conditionEngine.ValidateCondition(model.Condition);
                    if (validationResult != "OK")
                    {
                        ModelState.AddModelError("Condition", validationResult);
                        ViewBag.Templates = _conditionEngine.GetPredefinedTemplates();
                        ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables();
                        return View(model);
                    }

                    var alerta = new AutomatedAlert
                    {
                        Name = model.Name,
                        Description = model.Description,
                        Condition = model.Condition,
                        AlertType = model.AlertType,
                        Recipients = JsonConvert.SerializeObject(model.Recipients?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>()),
                        IsActive = model.IsActive,
                        Priority = model.Priority,
                        CreatedBy = User.Identity?.Name ?? "Sistema",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.AutomatedAlerts.Add(alerta);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Alerta criado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao criar alerta");
                    ModelState.AddModelError("", "Erro ao criar alerta. Tente novamente.");
                }
            }

            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables();
            return View(model);
        }

        // GET: /Alertas/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var alerta = await _context.AutomatedAlerts.FindAsync(id);
            if (alerta == null)
            {
                return NotFound();
            }

            var model = new CreateAlertViewModel
            {
                Name = alerta.Name,
                Description = alerta.Description,
                Condition = alerta.Condition,
                AlertType = alerta.AlertType,
                Recipients = string.Join(", ", JsonConvert.DeserializeObject<List<string>>(alerta.Recipients ?? "[]") ?? new List<string>()),
                IsActive = alerta.IsActive,
                Priority = alerta.Priority
            };

            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables();
            return View(model);
        }

        // POST: /Alertas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateAlertViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var alerta = await _context.AutomatedAlerts.FindAsync(id);
                    if (alerta == null)
                    {
                        return NotFound();
                    }

                    // Validar condição
                    var validationResult = _conditionEngine.ValidateCondition(model.Condition);
                    if (validationResult != "OK")
                    {
                        ModelState.AddModelError("Condition", validationResult);
                        ViewBag.Templates = _conditionEngine.GetPredefinedTemplates();
                        ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables();
                        return View(model);
                    }

                    alerta.Name = model.Name;
                    alerta.Description = model.Description;
                    alerta.Condition = model.Condition;
                    alerta.AlertType = model.AlertType;
                    alerta.Recipients = JsonConvert.SerializeObject(model.Recipients?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>());
                    alerta.IsActive = model.IsActive;
                    alerta.Priority = model.Priority;

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Alerta atualizado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao atualizar alerta {Id}", id);
                    ModelState.AddModelError("", "Erro ao atualizar alerta. Tente novamente.");
                }
            }

            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables();
            return View(model);
        }

        // POST: /Alertas/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var alerta = await _context.AutomatedAlerts.FindAsync(id);
                if (alerta == null)
                {
                    return NotFound();
                }

                _context.AutomatedAlerts.Remove(alerta);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Alerta excluído com sucesso!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir alerta {Id}", id);
                TempData["ErrorMessage"] = "Erro ao excluir alerta. Tente novamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Alertas/Toggle/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            try
            {
                var alerta = await _context.AutomatedAlerts.FindAsync(id);
                if (alerta == null)
                {
                    return NotFound();
                }

                alerta.IsActive = !alerta.IsActive;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Alerta {(alerta.IsActive ? "ativado" : "desativado")} com sucesso!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar status do alerta {Id}", id);
                TempData["ErrorMessage"] = "Erro ao alterar status do alerta. Tente novamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Alertas/Test/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(int id)
        {
            try
            {
                var alerta = await _context.AutomatedAlerts.FindAsync(id);
                if (alerta == null)
                {
                    return NotFound();
                }

                var result = await _alertService.EvaluateAlertConditionAsync(alerta);

                if (result)
                {
                    await _alertService.TriggerAlertAsync(alerta, new Dictionary<string, object>());
                    TempData["SuccessMessage"] = "Teste do alerta executado com sucesso! Condição atendida e alerta disparado.";
                }
                else
                {
                    TempData["InfoMessage"] = "Teste executado. Condição não foi atendida no momento atual.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar alerta {Id}", id);
                TempData["ErrorMessage"] = "Erro ao testar alerta. Verifique a condição.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Alertas/ValidateCondition
        [HttpGet]
        public IActionResult ValidateCondition(string condition)
        {
            var result = _conditionEngine.ValidateCondition(condition);
            return Json(new { isValid = result == "OK", message = result });
        }

        // GET: /Alertas/GetTemplates
        [HttpGet]
        public IActionResult GetTemplates()
        {
            var templates = _conditionEngine.GetPredefinedTemplates();
            return Json(templates);
        }

        // GET: /Alertas/GetVariables
        [HttpGet]
        public IActionResult GetVariables()
        {
            var variables = _conditionEngine.GetAvailableVariables();
            return Json(variables);
        }
    }

    /// <summary>
    /// ViewModel para criação/edição de alertas
    /// </summary>
    public class CreateAlertViewModel
    {
        [Required(ErrorMessage = "Nome é obrigatório")]
        [StringLength(100, ErrorMessage = "Nome deve ter no máximo 100 caracteres")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Descrição deve ter no máximo 500 caracteres")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Condição é obrigatória")]
        public string Condition { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipo de alerta é obrigatório")]
        public string AlertType { get; set; } = "EMAIL";

        public string? Recipients { get; set; }

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "Prioridade é obrigatória")]
        public string Priority { get; set; } = "MEDIUM";
    }
}
