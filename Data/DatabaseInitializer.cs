using Microsoft.EntityFrameworkCore;
using ClassificadorDoc.Data;

namespace ClassificadorDoc.Data
{
    /// <summary>
    /// Classe responsável pela inicialização automática do banco de dados
    /// Verifica se o banco existe, aplica migrations e executa seed se necessário
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Inicializa o banco de dados automaticamente
        /// </summary>
        /// <param name="app">A aplicação web</param>
        public static async Task InitializeAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();

                // Verificar se o banco de dados existe
                logger.LogInformation("🔍 Verificando se o banco de dados existe...");
                bool canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    logger.LogInformation("📊 Banco de dados não encontrado. Verificando se há migrations para aplicar...");

                    // Verificar se existem migrations
                    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        logger.LogInformation("🔄 Aplicando migrations para criar o banco de dados...");
                        await context.Database.MigrateAsync();
                        logger.LogInformation("✅ Banco de dados criado via migrations com sucesso!");
                    }
                    else
                    {
                        logger.LogInformation("📊 Criando banco de dados sem migrations...");
                        await context.Database.EnsureCreatedAsync();
                        logger.LogInformation("✅ Banco de dados criado com sucesso!");
                    }
                }
                else
                {
                    logger.LogInformation("📊 Banco de dados encontrado. Verificando migrations pendentes...");
                    // Aplicar migrations pendentes se houver
                    await ApplyPendingMigrationsAsync(context, logger);
                }                // Verificar se o banco está vazio e executar seed se necessário
                bool needsSeed = await CheckIfDatabaseNeedsSeedAsync(context, logger);

                if (needsSeed)
                {
                    logger.LogInformation("🌱 Banco de dados vazio detectado. Executando seed de dados...");
                    await SeedData.InitializeAsync(services);
                    logger.LogInformation("✅ Seed de dados executado com sucesso!");
                }
                else
                {
                    logger.LogInformation("✅ Banco de dados já contém dados. Seed não necessário.");
                }

                // Sempre verificar e atualizar configurações padrão
                await SeedConfiguracoesPadraoAsync(context, logger);

                logger.LogInformation("🎉 Inicialização do banco de dados concluída com sucesso!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Erro durante a inicialização do banco de dados: {Message}", ex.Message);

                // Em desenvolvimento, você pode querer parar a aplicação se houver erro no banco
                if (app.Environment.IsDevelopment())
                {
                    logger.LogCritical("🛑 Aplicação será interrompida devido ao erro no banco de dados em ambiente de desenvolvimento.");
                    throw;
                }

                logger.LogWarning("⚠️ Continuando a execução da aplicação apesar do erro no banco de dados...");
            }
        }

        /// <summary>
        /// Aplica migrations pendentes se houver
        /// </summary>
        private static async Task ApplyPendingMigrationsAsync(ApplicationDbContext context, ILogger logger)
        {
            try
            {
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

                if (pendingMigrations.Any())
                {
                    logger.LogInformation("🔄 Aplicando {Count} migrations pendentes: {Migrations}",
                        pendingMigrations.Count(), string.Join(", ", pendingMigrations));

                    await context.Database.MigrateAsync();
                    logger.LogInformation("✅ Migrations aplicadas com sucesso!");
                }
                else
                {
                    logger.LogInformation("✅ Nenhuma migration pendente encontrada. Banco de dados atualizado.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Erro ao aplicar migrations: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Verifica se o banco de dados precisa de seed inicial
        /// </summary>
        private static async Task<bool> CheckIfDatabaseNeedsSeedAsync(ApplicationDbContext context, ILogger logger)
        {
            try
            {
                // Verificar se existem usuários (indicador de que o seed já foi executado)
                bool hasUsers = await context.Users.AnyAsync();
                if (hasUsers)
                {
                    logger.LogDebug("👥 Usuários encontrados no banco. Seed não necessário.");
                    return false;
                }

                // Verificar se existem roles
                bool hasRoles = await context.Roles.AnyAsync();
                if (hasRoles)
                {
                    logger.LogDebug("🔐 Roles encontradas no banco. Seed não necessário.");
                    return false;
                }

                // Se não há usuários nem roles, precisa fazer seed
                logger.LogDebug("📭 Banco de dados vazio detectado. Seed necessário.");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️ Erro ao verificar se banco precisa de seed. Assumindo que precisa: {Message}", ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Seed das configurações padrão do sistema
        /// </summary>
        private static async Task SeedConfiguracoesPadraoAsync(ApplicationDbContext context, ILogger logger)
        {
            try
            {
                logger.LogInformation("⚙️ Verificando configurações padrão...");

                // Verificar se já existem configurações
                if (!await context.Configuracoes.AnyAsync())
                {
                    logger.LogInformation("🔧 Criando configurações padrão...");

                    var configuracoesPadrao = new[]
                    {
                        new Models.Configuracao
                        {
                            Chave = Models.ChavesConfiguracao.CAMINHO_SALVAMENTO_DOCUMENTOS,
                            Valor = string.Empty, // Vazio = usar pasta Documents
                            Descricao = "Caminho personalizado para salvamento dos documentos processados. Se vazio, usa a pasta Documents do usuário.",
                            Categoria = "Armazenamento",
                            Ativo = true
                        },
                        new Models.Configuracao
                        {
                            Chave = Models.ChavesConfiguracao.DIRETORIO_BASE_DOCUMENTOS,
                            Valor = "DocumentosProcessados",
                            Descricao = "Nome do diretório base onde os documentos serão organizados",
                            Categoria = "Armazenamento",
                            Ativo = true
                        },
                        new Models.Configuracao
                        {
                            Chave = Models.ChavesConfiguracao.NOME_PASTA_CLASSIFICADOR,
                            Valor = "ClassificadorDoc",
                            Descricao = "Nome da pasta principal do classificador de documentos",
                            Categoria = "Armazenamento",
                            Ativo = true
                        },
                        new Models.Configuracao
                        {
                            Chave = Models.ChavesConfiguracao.ESTRUTURA_PASTAS_HABILITADA,
                            Valor = "true",
                            Descricao = "Define se a estrutura de pastas por tipo de documento está habilitada",
                            Categoria = "Armazenamento",
                            Ativo = true
                        }
                    };

                    context.Configuracoes.AddRange(configuracoesPadrao);
                    await context.SaveChangesAsync();

                    logger.LogInformation("✅ Configurações padrão criadas com sucesso!");
                }
                else
                {
                    logger.LogDebug("⚙️ Configurações já existem no banco de dados.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Erro ao criar configurações padrão: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Método para verificar o status atual do banco de dados (útil para debugging)
        /// </summary>
        public static async Task<DatabaseStatus> GetDatabaseStatusAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var status = new DatabaseStatus();

            try
            {
                status.CanConnect = await context.Database.CanConnectAsync();

                if (status.CanConnect)
                {
                    status.PendingMigrations = await context.Database.GetPendingMigrationsAsync();
                    status.AppliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                    status.HasUsers = await context.Users.AnyAsync();
                    status.HasRoles = await context.Roles.AnyAsync();
                    status.UserCount = await context.Users.CountAsync();
                    status.RoleCount = await context.Roles.CountAsync();
                }
            }
            catch (Exception ex)
            {
                status.Error = ex.Message;
            }

            return status;
        }
    }

    /// <summary>
    /// Classe para representar o status do banco de dados
    /// </summary>
    public class DatabaseStatus
    {
        public bool CanConnect { get; set; }
        public IEnumerable<string> PendingMigrations { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> AppliedMigrations { get; set; } = Enumerable.Empty<string>();
        public bool HasUsers { get; set; }
        public bool HasRoles { get; set; }
        public int UserCount { get; set; }
        public int RoleCount { get; set; }
        public string? Error { get; set; }

        public bool IsHealthy => CanConnect && !PendingMigrations.Any() && string.IsNullOrEmpty(Error);
        public bool NeedsSeed => CanConnect && !HasUsers && !HasRoles;
    }
}
