using System.ComponentModel.DataAnnotations;

namespace ClassificadorDoc.Models
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Nome completo é obrigatório")]
        [StringLength(100, ErrorMessage = "Nome completo deve ter no máximo 100 caracteres")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Departamento deve ter no máximo 50 caracteres")]
        public string? Department { get; set; }

        [Required(ErrorMessage = "Papel é obrigatório")]
        public string Role { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha deve ter entre 6 e 100 caracteres")]
        public string Password { get; set; } = string.Empty;

        public bool SendEmailInvitation { get; set; } = true;
        public bool RequirePasswordChange { get; set; } = true;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome completo é obrigatório")]
        [StringLength(100, ErrorMessage = "Nome completo deve ter no máximo 100 caracteres")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Departamento deve ter no máximo 50 caracteres")]
        public string? Department { get; set; }

        public string Role { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public bool IsActive { get; set; } = true;
        public bool EmailConfirmed { get; set; } = true;
        public bool LockoutEnabled { get; set; } = false;
    }

    public class ChangePasswordViewModel
    {
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha atual é obrigatória")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nova senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Nova senha deve ter entre 6 e 100 caracteres")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        [Compare("NewPassword", ErrorMessage = "Nova senha e confirmação devem ser iguais")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "E-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nova senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Nova senha deve ter entre 6 e 100 caracteres")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        [Compare("NewPassword", ErrorMessage = "Nova senha e confirmação devem ser iguais")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
