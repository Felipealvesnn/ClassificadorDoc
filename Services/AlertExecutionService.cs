using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Quartz;

namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Serviço para execução automática de alertas usando Quartz.NET
    /// Atende ao requisito 4.2.6 - Alertas automáticos programáveis
    /// </summary>
    public interface IAlertExecutionService
    {
        Task ProcessActiveAlertsAsync();
        Task<bool> EvaluateAlertConditionAsync(AutomatedAlert alert);
        Task TriggerAlertAsync(AutomatedAlert alert, Dictionary<string, object> context);
    }

    public class AlertExecutionService : IAlertExecutionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AlertExecutionService> _logger;
        private readonly IEmailService _emailService;
        private readonly IAlertConditionEngine _conditionEngine;
        private readonly ISystemNotificationService _notificationService;

        public AlertExecutionService(
            ApplicationDbContext context,
            ILogger<AlertExecutionService> logger,
            IEmailService emailService,
            IAlertConditionEngine conditionEngine,
            ISystemNotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _conditionEngine = conditionEngine;
            _notificationService = notificationService;
        }

        public async Task ProcessActiveAlertsAsync()
        {
            try
            {
                var activeAlerts = await _context.AutomatedAlerts
                    .Where(a => a.IsActive)
                    .ToListAsync();

                _logger.LogInformation("Processando {Count} alertas ativos", activeAlerts.Count);

                foreach (var alert in activeAlerts)
                {
                    try
                    {
                        if (await EvaluateAlertConditionAsync(alert))
                        {
                            await TriggerAlertAsync(alert, new Dictionary<string, object>());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar alerta {AlertId}: {AlertName}",
                            alert.Id, alert.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro geral no processamento de alertas");
            }
        }

        public async Task<bool> EvaluateAlertConditionAsync(AutomatedAlert alert)
        {
            try
            {
                // Verificar se já foi disparado recentemente (evitar spam)
                if (alert.LastTriggered.HasValue &&
                    alert.LastTriggered.Value.AddMinutes(30) > DateTime.UtcNow)
                {
                    return false;
                }

                // Usar o engine de condições para avaliar
                var context = await BuildAlertContextAsync();
                return await _conditionEngine.EvaluateConditionAsync(alert.Condition, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao avaliar condição do alerta {AlertId}", alert.Id);
                return false;
            }
        }

        public async Task TriggerAlertAsync(AutomatedAlert alert, Dictionary<string, object> context)
        {
            try
            {
                _logger.LogInformation("Disparando alerta: {AlertName} ({AlertType})",
                    alert.Name, alert.AlertType);

                // Atualizar estatísticas do alerta
                alert.LastTriggered = DateTime.UtcNow;
                alert.TriggerCount++;
                alert.LastResult = "TRIGGERED";

                // Processar baseado no tipo de alerta
                switch (alert.AlertType.ToUpper())
                {
                    case "EMAIL":
                        await SendEmailAlertAsync(alert, context);
                        break;

                    case "SYSTEM":
                        await SendSystemNotificationAsync(alert, context);
                        break;

                    case "WEBHOOK":
                        await SendWebhookAlertAsync(alert, context);
                        break;

                    default:
                        _logger.LogWarning("Tipo de alerta não suportado: {AlertType}", alert.AlertType);
                        break;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao disparar alerta {AlertId}", alert.Id);
                alert.LastResult = $"ERROR: {ex.Message}";
                await _context.SaveChangesAsync();
            }
        }

        private async Task<Dictionary<string, object>> BuildAlertContextAsync()
        {
            // Construir contexto com dados atuais do sistema
            var context = new Dictionary<string, object>();

            // Usuários ativos
            var activeUsersCount = await _context.ActiveUserSessions
                .Where(s => s.IsActive && s.LastActivity > DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            // Documentos processados hoje
            var documentsToday = await _context.DocumentProcessingHistories
                .Where(d => d.ProcessedAt.Date == DateTime.Today)
                .CountAsync();

            // Taxa de erro hoje
            var todayDocuments = await _context.DocumentProcessingHistories
                .Where(d => d.ProcessedAt.Date == DateTime.Today)
                .ToListAsync();

            var errorRateToday = todayDocuments.Any()
                ? todayDocuments.Average(d => d.IsSuccessful ? 0.0 : 1.0) * 100
                : 0.0;

            // Lotes processados hoje
            var batchesToday = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt.Date == DateTime.Today)
                .CountAsync();

            context["active_users"] = activeUsersCount;
            context["documents_today"] = documentsToday;
            context["error_rate_today"] = errorRateToday;
            context["batches_today"] = batchesToday;
            context["current_hour"] = DateTime.Now.Hour;
            context["current_date"] = DateTime.Today;

            return context;
        }

        private async Task SendEmailAlertAsync(AutomatedAlert alert, Dictionary<string, object> context)
        {
            try
            {
                var recipients = JsonConvert.DeserializeObject<List<string>>(alert.Recipients ?? "[]");

                foreach (var recipient in recipients ?? new List<string>())
                {
                    var subject = $"[ALERTA] {alert.Name}";
                    var body = $@"
                        <h2>Alerta Disparado: {alert.Name}</h2>
                        <p><strong>Descrição:</strong> {alert.Description}</p>
                        <p><strong>Prioridade:</strong> {alert.Priority}</p>
                        <p><strong>Data/Hora:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                        <hr>
                        <p>Este é um alerta automático do sistema ClassificadorDoc.</p>
                    ";

                    await _emailService.SendEmailAsync(recipient, subject, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar email para alerta {AlertId}", alert.Id);
            }
        }

        private async Task SendSystemNotificationAsync(AutomatedAlert alert, Dictionary<string, object> context)
        {
            try
            {
                var priorityMapping = alert.Priority switch
                {
                    "HIGH" => "HIGH",
                    "LOW" => "LOW",
                    _ => "NORMAL"
                };

                var title = $"🚨 Alerta: {alert.Name}";
                var message = !string.IsNullOrEmpty(alert.Description)
                    ? alert.Description
                    : "Condição de alerta foi atendida. Verifique o sistema.";

                // Adicionar contexto relevante na mensagem
                if (context.Any())
                {
                    var contextInfo = string.Join(", ",
                        context.Take(3).Select(kv => $"{kv.Key}: {kv.Value}"));
                    message += $"\n\nContexto: {contextInfo}";
                }

                var actionUrl = $"/Alertas/Details/{alert.Id}";

                // Usar o sistema existente de notificações para todos os administradores
                await _notificationService.CreateNotificationAsync(
                    title: title,
                    message: message,
                    type: "ALERT",
                    priority: priorityMapping,
                    userId: null, // null = todos os administradores
                    alertId: alert.Id,
                    playSound: alert.Priority == "HIGH",
                    showToast: true,
                    actionUrl: actionUrl
                );

                _logger.LogInformation("Notificação de sistema criada para alerta: {AlertName}", alert.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar notificação de sistema para alerta {AlertId}", alert.Id);
            }
        }

        private async Task SendWebhookAlertAsync(AutomatedAlert alert, Dictionary<string, object> context)
        {
            // Implementar webhook (para ser implementado)
            _logger.LogInformation("Webhook disparado para alerta: {AlertName}", alert.Name);

            // TODO: Implementar chamadas HTTP para webhooks
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Job do Quartz.NET para execução periódica dos alertas
    /// </summary>
    [DisallowConcurrentExecution]
    public class AlertExecutionJob : IJob
    {
        private readonly IAlertExecutionService _alertService;
        private readonly ILogger<AlertExecutionJob> _logger;

        public AlertExecutionJob(IAlertExecutionService alertService, ILogger<AlertExecutionJob> logger)
        {
            _alertService = alertService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Iniciando execução de alertas automáticos");

            try
            {
                await _alertService.ProcessActiveAlertsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na execução do job de alertas");
            }

            _logger.LogInformation("Execução de alertas automáticos finalizada");
        }
    }
}
