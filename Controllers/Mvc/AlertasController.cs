using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using ClassificadorDoc.ViewModels;
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
            try
            {
                ViewBag.Templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
                ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar templates e variáveis");
                ViewBag.Templates = new List<AlertTemplate>();
                ViewBag.AvailableVariables = new List<dynamic>();
            }
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
                        ViewBag.Templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
                        ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
                        return View(model);
                    }

                    var recipientsList = model.Recipients?.Split(',')
                        .Select(r => r.Trim())
                        .Where(r => !string.IsNullOrEmpty(r))
                        .ToList() ?? new List<string>();

                    var alerta = new AutomatedAlert
                    {
                        Name = model.Name,
                        Description = model.Description,
                        Condition = model.Condition,
                        AlertType = model.AlertType,
                        Recipients = JsonConvert.SerializeObject(recipientsList),
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

            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
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

            string recipientsString = "";
            try
            {
                if (!string.IsNullOrEmpty(alerta.Recipients))
                {
                    if (alerta.Recipients.StartsWith("["))
                    {
                        var recipientsList = JsonConvert.DeserializeObject<List<string>>(alerta.Recipients) ?? new List<string>();
                        recipientsString = string.Join(", ", recipientsList);
                    }
                    else
                    {
                        recipientsString = alerta.Recipients;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao deserializar recipients do alerta {Id}", id);
                recipientsString = alerta.Recipients ?? "";
            }

            var model = new CreateAlertViewModel
            {
                Name = alerta.Name,
                Description = alerta.Description,
                Condition = alerta.Condition,
                AlertType = alerta.AlertType,
                Recipients = recipientsString,
                IsActive = alerta.IsActive,
                Priority = alerta.Priority
            };

            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
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
                        ViewBag.Templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
                        ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
                        return View(model);
                    }

                    alerta.Name = model.Name;
                    alerta.Description = model.Description;
                    alerta.Condition = model.Condition;
                    alerta.AlertType = model.AlertType;

                    var recipientsList = model.Recipients?.Split(',')
                        .Select(r => r.Trim())
                        .Where(r => !string.IsNullOrEmpty(r))
                        .ToList() ?? new List<string>();

                    alerta.Recipients = JsonConvert.SerializeObject(recipientsList);
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

            ViewBag.Templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
            ViewBag.AvailableVariables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
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
            try
            {
                if (string.IsNullOrWhiteSpace(condition))
                {
                    return Json(new { isValid = false, message = "Condição não pode estar vazia" });
                }

                var result = _conditionEngine.ValidateCondition(condition);
                return Json(new { isValid = result == "OK", message = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar condição: {Condition}", condition);
                return Json(new { isValid = false, message = "Erro interno na validação" });
            }
        }

        // GET: /Alertas/GetTemplates
        [HttpGet]
        public IActionResult GetTemplates()
        {
            var templates = _conditionEngine.GetPredefinedTemplates() ?? new List<AlertTemplate>();
            return Json(templates);
        }

        // GET: /Alertas/GetVariables
        [HttpGet]
        public IActionResult GetVariables()
        {
            var variables = _conditionEngine.GetAvailableVariables() ?? new List<dynamic>();
            return Json(variables);
        }

        // POST: /Alertas/FixRecipientsData (método para corrigir dados existentes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FixRecipientsData()
        {
            try
            {
                var alertas = await _context.AutomatedAlerts.ToListAsync();
                int fixedCount = 0;

                foreach (var alerta in alertas)
                {
                    if (string.IsNullOrEmpty(alerta.Recipients))
                    {
                        alerta.Recipients = "[]";
                        fixedCount++;
                    }
                    else if (!alerta.Recipients.StartsWith("[") && !alerta.Recipients.StartsWith("{"))
                    {
                        // Converter string simples para JSON
                        var recipientsList = alerta.Recipients.Split(',')
                            .Select(r => r.Trim())
                            .Where(r => !string.IsNullOrEmpty(r))
                            .ToList();
                        alerta.Recipients = JsonConvert.SerializeObject(recipientsList);
                        fixedCount++;
                    }
                }

                if (fixedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Dados de {fixedCount} alertas foram corrigidos com sucesso!";
                }
                else
                {
                    TempData["InfoMessage"] = "Todos os dados já estão no formato correto.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao corrigir dados dos recipients");
                TempData["ErrorMessage"] = "Erro ao corrigir dados. Tente novamente.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
