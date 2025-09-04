using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize(Roles = "Administrator")]
    public class RelatoriosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RelatoriosController> _logger;

        public RelatoriosController(ApplicationDbContext context, ILogger<RelatoriosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Relatorios
        public async Task<IActionResult> Index()
        {
            var stats = await GetDashboardStats();
            return View(stats);
        }

        // GET: /Relatorios/Auditoria
        public async Task<IActionResult> Auditoria(DateTime? startDate = null, DateTime? endDate = null, string? userId = null)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today.AddDays(1);

            var logs = await _context.AuditLogs
                .Where(a => a.Timestamp >= startDate && a.Timestamp < endDate)
                .Where(a => string.IsNullOrEmpty(userId) || a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(1000)
                .ToListAsync();

            var model = new AuditReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value.AddDays(-1),
                UserId = userId,
                Logs = logs,
                TotalRecords = logs.Count
            };

            ViewBag.Users = await _context.Users
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync();

            return View(model);
        }

        // GET: /Relatorios/Produtividade
        public async Task<IActionResult> Produtividade(DateTime? date = null)
        {
            date ??= DateTime.Today;

            var productivities = await _context.UserProductivities
                .Where(p => p.Date.Date == date.Value.Date)
                .ToListAsync();

            ViewBag.SelectedDate = date.Value;
            return View(productivities);
        }

        // GET: /Relatorios/UsuariosConectados
        public async Task<IActionResult> UsuariosConectados()
        {
            var activeSessions = await _context.ActiveUserSessions
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.LastActivity)
                .ToListAsync();

            return View(activeSessions);
        }

        // GET: /Relatorios/Exportar
        public IActionResult Exportar()
        {
            return View();
        }

        // POST: /Relatorios/Exportar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Exportar(string dataType, string format, DateTime startDate, DateTime endDate)
        {
            try
            {
                var export = new DataExport
                {
                    ExportName = $"{dataType}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Format = format.ToUpper(),
                    DataType = dataType,
                    UserId = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "",
                    RequestedAt = DateTime.UtcNow,
                    Status = "PROCESSING"
                };

                _context.DataExports.Add(export);
                await _context.SaveChangesAsync();

                // Aqui você implementaria a lógica real de exportação
                // Por enquanto, vamos simular sucesso
                export.Status = "COMPLETED";
                export.CompletedAt = DateTime.UtcNow;
                export.RecordCount = 100; // Exemplo
                export.FilePath = $"/exports/{export.ExportName}.{format.ToLower()}";

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Exportação iniciada! O arquivo estará disponível em breve.";
                return RedirectToAction("Exportar");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar exportação");
                TempData["ErrorMessage"] = "Erro ao processar exportação.";
                return RedirectToAction("Exportar");
            }
        }

        private async Task<DashboardStatsViewModel> GetDashboardStats()
        {
            var today = DateTime.Today;
            var thirtyDaysAgo = today.AddDays(-30);

            var stats = new DashboardStatsViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsers = await _context.ActiveUserSessions.CountAsync(s => s.IsActive),
                TotalDocuments = await _context.DocumentProcessingHistories.CountAsync(),
                DocumentsToday = await _context.DocumentProcessingHistories
                    .CountAsync(d => d.ProcessedAt.Date == today),
                AuditLogsCount = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= thirtyDaysAgo),
                SecurityEvents = await _context.AuditLogs
                    .CountAsync(a => a.Category == "SECURITY" && a.Timestamp >= thirtyDaysAgo)
            };

            return stats;
        }
    }

    public class DashboardStatsViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalDocuments { get; set; }
        public int DocumentsToday { get; set; }
        public int AuditLogsCount { get; set; }
        public int SecurityEvents { get; set; }
    }
}
