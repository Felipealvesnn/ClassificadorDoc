using Microsoft.AspNetCore.Identity;
using ClassificadorDoc.Data;

namespace ClassificadorDoc.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Criar roles se não existirem
            await CreateRoleIfNotExistsAsync(roleManager, "Admin", "Administrador do sistema com acesso total");
            await CreateRoleIfNotExistsAsync(roleManager, "User", "Usuário comum com acesso básico");
            await CreateRoleIfNotExistsAsync(roleManager, "Classifier", "Usuário especializado em classificação de documentos");

            // Criar usuários padrão se não existirem
            await CreateUserIfNotExistsAsync(userManager, "admin@classificador.com", "Admin@123", "Administrador", "Administração", "Admin");
            await CreateUserIfNotExistsAsync(userManager, "usuario@classificador.com", "User@123", "Usuário Comum", "Operações", "User");
            await CreateUserIfNotExistsAsync(userManager, "classificador@classificador.com", "Class@123", "Especialista em Classificação", "Documentos", "Classifier");
        }

        private static async Task CreateRoleIfNotExistsAsync(RoleManager<ApplicationRole> roleManager, string roleName, string description)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new ApplicationRole
                {
                    Name = roleName,
                    Description = description
                };
                await roleManager.CreateAsync(role);
            }
        }

        private static async Task CreateUserIfNotExistsAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string fullName,
            string department,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    Department = department,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}
