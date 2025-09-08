using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClassificadorDoc.Models;
using ClassificadorDoc.Models.ViewModels;
using ClassificadorDoc.Data;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["UserName"] = User.Identity?.Name ?? "Usuário";
            ViewData["IsAdmin"] = User.IsInRole("Admin");

            try
            {
                var dashboardData = await GetDashboardDataAsync();
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar dados do dashboard");
                // Em caso de erro, retorna view com dados vazios
                return View(new DashboardViewModel());
            }
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

        private async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            var dashboard = new DashboardViewModel();

            // Buscar dados dos últimos 30 dias
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            // 1. Estatísticas gerais
            var allDocuments = await _context.DocumentProcessingHistories
                .Where(d => d.ProcessedAt >= thirtyDaysAgo)
                .ToListAsync();

            var totalDocuments = await _context.DocumentProcessingHistories.CountAsync();
            var successfulDocuments = await _context.DocumentProcessingHistories
                .Where(d => !string.IsNullOrEmpty(d.DocumentType))
                .CountAsync();

            dashboard.Stats = new DashboardStats
            {
                TotalDocuments = totalDocuments,
                SuccessRate = totalDocuments > 0 ? (decimal)successfulDocuments / totalDocuments * 100 : 0,
                ActiveUsers = await _context.Users.CountAsync(), // Tabela do Identity
                ProcessingCount = 0 // Assumindo que não há processamento em background no momento
            };

            // 2. Atividade recente (últimos 10 documentos)
            var recentDocs = await _context.DocumentProcessingHistories
                .OrderByDescending(d => d.ProcessedAt)
                .Take(10)
                .ToListAsync();

            dashboard.RecentActivities = recentDocs.Select(doc => new RecentActivity
            {
                FileName = doc.FileName ?? "Documento sem nome",
                Classification = doc.DocumentType ?? "Não classificado",
                ProcessedAt = doc.ProcessedAt,
                FileExtension = Path.GetExtension(doc.FileName ?? "").ToLower(),
                TimeAgo = GetTimeAgo(doc.ProcessedAt),
                BadgeClass = GetBadgeClass(doc.DocumentType),
                IconClass = GetIconClass(Path.GetExtension(doc.FileName ?? ""))
            }).ToList();

            // 3. Dados do gráfico (últimos 7 dias)
            var sevenDaysAgo = DateTime.Now.AddDays(-7);
            var chartData = await _context.DocumentProcessingHistories
                .Where(d => d.ProcessedAt >= sevenDaysAgo)
                .GroupBy(d => d.ProcessedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            dashboard.ChartData = new ChartData
            {
                Labels = chartData.Select(c => c.Date.ToString("dd/MM")).ToList(),
                Values = chartData.Select(c => c.Count).ToList()
            };

            // 4. Estatísticas por tipo
            var typeStats = await _context.DocumentProcessingHistories
                .Where(d => !string.IsNullOrEmpty(d.DocumentType))
                .GroupBy(d => d.DocumentType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(4)
                .ToListAsync();

            var totalTypeDocs = typeStats.Sum(t => t.Count);
            var progressBarClasses = new[] { "bg-primary", "bg-success", "bg-info", "bg-warning" };

            dashboard.TypeStatistics = typeStats.Select((stat, index) => new TypeStatistic
            {
                Type = stat.Type ?? "Outros",
                Count = stat.Count,
                Percentage = totalTypeDocs > 0 ? (decimal)stat.Count / totalTypeDocs * 100 : 0,
                ProgressBarClass = progressBarClasses[index % progressBarClasses.Length]
            }).ToList();

            return dashboard;
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "Agora mesmo";
            if (timeSpan.TotalMinutes < 60)
                return $"Há {(int)timeSpan.TotalMinutes} minuto{((int)timeSpan.TotalMinutes == 1 ? "" : "s")}";
            if (timeSpan.TotalHours < 24)
                return $"Há {(int)timeSpan.TotalHours} hora{((int)timeSpan.TotalHours == 1 ? "" : "s")}";
            if (timeSpan.TotalDays < 7)
                return $"Há {(int)timeSpan.TotalDays} dia{((int)timeSpan.TotalDays == 1 ? "" : "s")}";

            return dateTime.ToString("dd/MM/yyyy");
        }

        private string GetBadgeClass(string? classification)
        {
            return classification?.ToLower() switch
            {
                var c when c?.Contains("contrato") == true => "bg-success",
                var c when c?.Contains("fiscal") == true => "bg-warning",
                var c when c?.Contains("relatório") == true || c?.Contains("relatorio") == true => "bg-info",
                var c when c?.Contains("proposta") == true => "bg-primary",
                _ => "bg-secondary"
            };
        }

        private string GetIconClass(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "fas fa-file-pdf text-danger",
                ".doc" or ".docx" => "fas fa-file-word text-primary",
                ".xls" or ".xlsx" => "fas fa-file-excel text-success",
                ".ppt" or ".pptx" => "fas fa-file-powerpoint text-warning",
                _ => "fas fa-file text-secondary"
            };
        }
    }
}
