using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Serviço de auditoria para compliance com requisitos do edital
    /// Registra todas as atividades do sistema com retenção mínima de 12 meses
    /// </summary>
    public interface IAuditService
    {
        Task LogAsync(string action, string resource, string result = "SUCCESS", object? details = null, string category = "ACCESS", string severity = "LOW");
        Task LogSecurityEventAsync(string action, string resource, string result, string? errorMessage = null, object? details = null);
        Task LogBusinessActionAsync(string action, string resource, object? details = null);
        Task LogAdminActionAsync(string action, string resource, object? details = null);
        Task<AuditReportViewModel> GetAuditReportAsync(DateTime startDate, DateTime endDate, string? userId = null, string? action = null, string? category = null, int page = 1, int pageSize = 50);
        Task<AuditStatsViewModel> GetAuditStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task CleanupOldLogsAsync(int retentionMonths = 12);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(string action, string resource, string result = "SUCCESS", object? details = null, string category = "ACCESS", string severity = "LOW")
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var userId = httpContext?.User?.Identity?.Name ?? "Anonymous";
                var userIdClaim = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
                var ipAddress = GetClientIpAddress();
                var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "";

                var auditLog = new AuditLog
                {
                    Timestamp = DateTime.UtcNow,
                    Action = action,
                    Resource = resource,
                    UserId = userIdClaim,
                    UserName = userId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent.Length > 500 ? userAgent.Substring(0, 500) : userAgent,
                    Details = details != null ? JsonSerializer.Serialize(details) : null,
                    Result = result,
                    Category = category,
                    Severity = severity
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Nunca falhar a operação principal por causa de auditoria
                _logger.LogError(ex, "Erro ao registrar log de auditoria: {Action} - {Resource}", action, resource);
            }
        }

        public async Task LogSecurityEventAsync(string action, string resource, string result, string? errorMessage = null, object? details = null)
        {
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                Resource = resource,
                UserId = GetCurrentUserId(),
                UserName = GetCurrentUserName(),
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                Result = result,
                ErrorMessage = errorMessage,
                Category = "SECURITY",
                Severity = result == "FAILED" ? "HIGH" : "MEDIUM"
            };

            try
            {
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao registrar evento de segurança");
            }
        }

        public async Task LogBusinessActionAsync(string action, string resource, object? details = null)
        {
            await LogAsync(action, resource, "SUCCESS", details, "BUSINESS", "MEDIUM");
        }

        public async Task LogAdminActionAsync(string action, string resource, object? details = null)
        {
            await LogAsync(action, resource, "SUCCESS", details, "ADMIN", "HIGH");
        }

        public async Task<AuditReportViewModel> GetAuditReportAsync(DateTime startDate, DateTime endDate, string? userId = null, string? action = null, string? category = null, int page = 1, int pageSize = 50)
        {
            var query = _context.AuditLogs.AsQueryable();

            query = query.Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.UserId == userId || a.UserName.Contains(userId));

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action.Contains(action));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(a => a.Category == category);

            var totalRecords = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new AuditReportViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                UserId = userId,
                Action = action,
                Category = category,
                Logs = logs,
                TotalRecords = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AuditStatsViewModel> GetAuditStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-1);
            endDate ??= DateTime.UtcNow;

            var query = _context.AuditLogs.Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate);

            var stats = new AuditStatsViewModel
            {
                TotalLogins = await query.CountAsync(a => a.Action == "LOGIN"),
                FailedLogins = await query.CountAsync(a => a.Action == "LOGIN" && a.Result == "FAILED"),
                DocumentsProcessed = await query.CountAsync(a => a.Action == "CLASSIFY_DOCUMENT"),
                AdminActions = await query.CountAsync(a => a.Category == "ADMIN"),
                SecurityEvents = await query.CountAsync(a => a.Category == "SECURITY"),
                OldestLog = await _context.AuditLogs.MinAsync(a => a.Timestamp),
                NewestLog = await _context.AuditLogs.MaxAsync(a => a.Timestamp)
            };

            // Resumo diário
            stats.DailySummary = await query
                .GroupBy(a => a.Timestamp.Date)
                .Select(g => new DailyAuditSummary
                {
                    Date = g.Key,
                    TotalActions = g.Count(),
                    UniqueUsers = g.Select(a => a.UserId).Distinct().Count(),
                    SecurityEvents = g.Count(a => a.Category == "SECURITY")
                })
                .OrderBy(d => d.Date)
                .ToListAsync();

            return stats;
        }

        public async Task CleanupOldLogsAsync(int retentionMonths = 12)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddMonths(-retentionMonths);
                var oldLogs = _context.AuditLogs.Where(a => a.Timestamp < cutoffDate);

                var count = await oldLogs.CountAsync();
                if (count > 0)
                {
                    _context.AuditLogs.RemoveRange(oldLogs);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Limpeza de auditoria concluída: {Count} registros removidos (mais antigos que {CutoffDate})", count, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na limpeza de logs de auditoria");
            }
        }

        private string GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        }

        private string GetCurrentUserName()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
        }

        private string GetClientIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return "Unknown";

            var xForwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private string GetUserAgent()
        {
            var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "";
            return userAgent.Length > 500 ? userAgent.Substring(0, 500) : userAgent;
        }
    }
}
