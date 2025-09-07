using System.ComponentModel.DataAnnotations;

namespace ClassificadorDoc.ViewModels
{
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
