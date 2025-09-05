namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Interface para serviço de email
    /// </summary>
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendEmailAsync(List<string> recipients, string subject, string body);
    }

    /// <summary>
    /// Implementação básica do serviço de email (simulação)
    /// Em produção, integrar com SendGrid, SMTP, etc.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            await SendEmailAsync(new List<string> { to }, subject, body);
        }

        public async Task SendEmailAsync(List<string> recipients, string subject, string body)
        {
            // Simulação de envio de email
            // Em produção, implementar com provider real
            foreach (var recipient in recipients)
            {
                _logger.LogInformation("EMAIL SIMULADO - Para: {Recipient}, Assunto: {Subject}, Corpo: {Body}",
                    recipient, subject, body);
            }

            // Simular delay de envio
            await Task.Delay(100);

            _logger.LogInformation("Email enviado para {Count} destinatários", recipients.Count);
        }
    }
}
