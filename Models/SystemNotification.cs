using System.ComponentModel.DataAnnotations;
using ClassificadorDoc.Data;

namespace ClassificadorDoc.Models
{
    /// <summary>
    /// Modelo para notificações do sistema
    /// </summary>
    public class SystemNotification
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = "INFO"; // INFO, SUCCESS, WARNING, ERROR, ALERT

        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "NORMAL"; // LOW, NORMAL, HIGH, URGENT

        public string? UserId { get; set; } // Se null, é para todos os usuários
        public ApplicationUser? User { get; set; }

        public int? AlertId { get; set; } // Referência ao alerta que gerou a notificação
        public AutomatedAlert? Alert { get; set; }

        public bool IsRead { get; set; } = false;
        public bool IsDisplayed { get; set; } = false; // Se já foi exibida como toast
        public bool PlaySound { get; set; } = true;
        public bool ShowToast { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        [StringLength(500)]
        public string? ActionUrl { get; set; } // URL para ação quando clicada

        [StringLength(50)]
        public string? Icon { get; set; } = "fas fa-info-circle";

        [StringLength(20)]
        public string? Color { get; set; } = "primary";

        public string? Metadata { get; set; } // JSON com dados extras
    }
}
