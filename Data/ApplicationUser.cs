using Microsoft.AspNetCore.Identity;

namespace ClassificadorDoc.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Department { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Propriedades específicas para classificação de documentos
        public int DocumentsProcessed { get; set; } = 0;
        public DateTime? LastDocumentProcessedAt { get; set; }
    }

    public class ApplicationRole : IdentityRole
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
