using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClassificadorDoc.Data;
using ClassificadorDoc.Models;

namespace ClassificadorDoc.Data
{
    /// <summary>
    /// Classe responsável por popular o banco de dados com dados iniciais de exemplo
    /// </summary>
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

            try
            {
                logger.LogInformation("🌱 Iniciando seed completo do banco de dados...");

                // 1. Criar roles
                await SeedRolesAsync(roleManager, logger);

                // 2. Criar usuários
                var users = await SeedUsersAsync(userManager, logger);

                // 3. Salvar mudanças antes de continuar
                await context.SaveChangesAsync();

                // 4. Popular outras tabelas (só se não existirem dados)
                await SeedDocumentProcessingHistoryAsync(context, users, logger);
                await SeedClassificationSessionsAsync(context, users, logger);
                await SeedBatchProcessingHistoryAsync(context, users, logger);
                await SeedAuditLogsAsync(context, users, logger);
                await SeedUserProductivityAsync(context, users, logger);
                await SeedActiveUserSessionsAsync(context, users, logger);
                await SeedDataMiningMetadataAsync(context, logger);
                await SeedTimeSeriesDataAsync(context, logger);
                await SeedAutomatedAlertsAsync(context, users, logger);
                await SeedDashboardWidgetsAsync(context, users, logger);
                await SeedLGPDComplianceAsync(context, users, logger);
                await SeedDataExportsAsync(context, users, logger);
                await SeedSystemNotificationsAsync(context, users, logger);

                await context.SaveChangesAsync();
                logger.LogInformation("✅ Seed completo do banco de dados finalizado com sucesso!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Erro durante o seed do banco de dados: {Message}", ex.Message);
                throw;
            }
        }

        #region Roles e Usuários
        private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
        {
            logger.LogInformation("👥 Criando roles do sistema...");

            var roles = new[]
            {
                new { Name = "Admin", Description = "Administrador do sistema com acesso total" },
                new { Name = "User", Description = "Usuário comum com acesso básico" },
                new { Name = "Classifier", Description = "Usuário especializado em classificação de documentos" },
                new { Name = "Manager", Description = "Gerente com acesso a relatórios e análises" },
                new { Name = "Auditor", Description = "Auditor com acesso a logs e conformidade" }
            };

            foreach (var roleData in roles)
            {
                await CreateRoleIfNotExistsAsync(roleManager, roleData.Name, roleData.Description);
            }
        }

        private static async Task<List<ApplicationUser>> SeedUsersAsync(UserManager<ApplicationUser> userManager, ILogger logger)
        {
            logger.LogInformation("👤 Criando usuários de exemplo...");

            var usersData = new[]
            {
                new { Email = "admin@classificador.com", Password = "Admin@123", FullName = "João Silva", Department = "Administração", Role = "Admin" },
                new { Email = "gerente@classificador.com", Password = "Manager@123", FullName = "Maria Santos", Department = "Gestão", Role = "Manager" },
                new { Email = "usuario1@classificador.com", Password = "User@123", FullName = "Pedro Oliveira", Department = "Operações", Role = "User" },
                new { Email = "usuario2@classificador.com", Password = "User@123", FullName = "Ana Costa", Department = "Operações", Role = "User" },
                new { Email = "classificador1@classificador.com", Password = "Class@123", FullName = "Carlos Ferreira", Department = "Documentos", Role = "Classifier" },
                new { Email = "classificador2@classificador.com", Password = "Class@123", FullName = "Lucia Rodrigues", Department = "Documentos", Role = "Classifier" },
                new { Email = "auditor@classificador.com", Password = "Audit@123", FullName = "Roberto Almeida", Department = "Auditoria", Role = "Auditor" }
            };

            var users = new List<ApplicationUser>();

            foreach (var userData in usersData)
            {
                var user = await CreateUserIfNotExistsAsync(userManager, userData.Email, userData.Password,
                    userData.FullName, userData.Department, userData.Role);
                if (user != null)
                {
                    users.Add(user);
                }
            }

            return users;
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

        private static async Task<ApplicationUser?> CreateUserIfNotExistsAsync(
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
                    return user;
                }
            }
            return user;
        }
        #endregion

        #region Histórico de Documentos
        private static async Task SeedDocumentProcessingHistoryAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.DocumentProcessingHistories.AnyAsync())
            {
                logger.LogDebug("Histórico de documentos já existe, pulando seed...");
                return;
            }

            logger.LogInformation("📄 Criando histórico de processamento de documentos...");

            var random = new Random();
            var documentTypes = new[] { "autuacao", "defesa", "notificacao_penalidade", "notificacao_autuacao", "outros" };
            var sampleFiles = new[]
            {
                "AIT_001234.pdf", "Defesa_Recurso_5678.pdf", "NIP_9876.pdf",
                "Notificacao_Autuacao_4321.pdf", "Documento_Anexo_1111.pdf",
                "Auto_Infracao_2023_001.pdf", "Recurso_JARI_2023_456.pdf",
                "Penalidade_Confirmada_789.pdf", "FICI_Identificacao_321.pdf"
            };

            var documents = new List<DocumentProcessingHistory>();

            for (int i = 0; i < 50; i++)
            {
                var user = users[random.Next(users.Count)];
                var docType = documentTypes[random.Next(documentTypes.Length)];
                var fileName = sampleFiles[random.Next(sampleFiles.Length)];

                documents.Add(new DocumentProcessingHistory
                {
                    FileName = $"{i:000}_{fileName}",
                    DocumentType = docType,
                    Confidence = Math.Round(random.NextDouble() * 0.4 + 0.6, 2), // 0.6 a 1.0
                    ProcessedAt = DateTime.UtcNow.AddDays(-random.Next(30)).AddHours(-random.Next(24)),
                    UserId = user.Id,
                    IsSuccessful = random.Next(10) > 1, // 90% sucesso
                    ErrorMessage = random.Next(10) <= 1 ? "Erro de stream position" : null,
                    Keywords = GetKeywordsForDocType(docType),
                    FileSizeBytes = random.Next(50000, 2000000) // 50KB a 2MB
                });
            }

            context.DocumentProcessingHistories.AddRange(documents);
        }

        private static string GetKeywordsForDocType(string docType)
        {
            return docType switch
            {
                "autuacao" => "auto de infração, AIT, agente de trânsito, lavrado",
                "defesa" => "defesa, recurso, JARI, contestação, impugnação",
                "notificacao_penalidade" => "NIP, notificação penalidade, pagamento, multa",
                "notificacao_autuacao" => "notificação autuação, cientificar, FICI",
                _ => "documento, trânsito, identificação"
            };
        }
        #endregion

        #region Sessões de Classificação
        private static async Task SeedClassificationSessionsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.ClassificationSessions.AnyAsync())
            {
                logger.LogDebug("Sessões de classificação já existem, pulando seed...");
                return;
            }

            logger.LogInformation("📊 Criando sessões de classificação...");

            var random = new Random();
            var sessions = new List<ClassificationSession>();

            for (int i = 0; i < 20; i++)
            {
                var user = users[random.Next(users.Count)];
                var startedAt = DateTime.UtcNow.AddDays(-random.Next(15)).AddHours(-random.Next(24));
                var totalDocs = random.Next(5, 25);
                var processedDocs = totalDocs - random.Next(0, 3);
                var successfulDocs = processedDocs - random.Next(0, 2);

                sessions.Add(new ClassificationSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    StartedAt = startedAt,
                    CompletedAt = startedAt.AddMinutes(random.Next(10, 120)),
                    TotalDocuments = totalDocs,
                    ProcessedDocuments = processedDocs,
                    SuccessfulDocuments = successfulDocs,
                    ProcessingMethod = random.Next(2) == 0 ? "visual" : "text",
                    UserId = user.Id,
                    ProcessingDuration = TimeSpan.FromMinutes(random.Next(10, 120))
                });
            }

            context.ClassificationSessions.AddRange(sessions);
        }
        #endregion

        #region Lotes de Processamento
        private static async Task SeedBatchProcessingHistoryAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.BatchProcessingHistories.AnyAsync())
            {
                logger.LogDebug("Histórico de lotes já existe, pulando seed...");
                return;
            }

            logger.LogInformation("📦 Criando histórico de lotes...");

            var random = new Random();
            var batches = new List<BatchProcessingHistory>();
            var docTypes = new[] { "autuacao", "defesa", "notificacao_penalidade", "notificacao_autuacao" };
            var statuses = new[] { "Completed", "Failed", "Processing" };

            for (int i = 0; i < 15; i++)
            {
                var user = users[random.Next(users.Count)];
                var startedAt = DateTime.UtcNow.AddDays(-random.Next(10));

                batches.Add(new BatchProcessingHistory
                {
                    BatchName = $"Lote_{DateTime.Now.Year}_{i + 1:000}",
                    StartedAt = startedAt,
                    CompletedAt = startedAt.AddMinutes(random.Next(30, 180)),
                    TotalDocuments = random.Next(10, 50),
                    SuccessfulDocuments = random.Next(5, 40),
                    FailedDocuments = random.Next(0, 5),
                    UserId = user.Id,
                    UserName = user.FullName ?? "Usuário",
                    ProcessingMethod = "visual",
                    Status = statuses[random.Next(statuses.Length)],
                    PredominantDocumentType = docTypes[random.Next(docTypes.Length)],
                    IpAddress = $"192.168.1.{random.Next(1, 255)}",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
                });
            }

            context.BatchProcessingHistories.AddRange(batches);
        }
        #endregion

        #region Logs de Auditoria
        private static async Task SeedAuditLogsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.AuditLogs.AnyAsync())
            {
                logger.LogDebug("Logs de auditoria já existem, pulando seed...");
                return;
            }

            logger.LogInformation("📋 Criando logs de auditoria...");

            var random = new Random();
            var actions = new[] { "Login", "Logout", "ProcessDocument", "ViewReport", "ExportData", "ChangeSettings" };
            var resources = new[] { "Sistema", "Documento", "Relatório", "Configuração", "Usuário" };
            var results = new[] { "Success", "Failed" };
            var categories = new[] { "Authentication", "DocumentProcessing", "DataAccess", "SystemConfig" };
            var severities = new[] { "Low", "Medium", "High" };

            var auditLogs = new List<AuditLog>();

            for (int i = 0; i < 100; i++)
            {
                var user = users[random.Next(users.Count)];
                var isSuccess = random.Next(10) > 1; // 90% sucesso

                auditLogs.Add(new AuditLog
                {
                    Timestamp = DateTime.UtcNow.AddDays(-random.Next(30)).AddMinutes(-random.Next(1440)),
                    Action = actions[random.Next(actions.Length)],
                    Resource = resources[random.Next(resources.Length)],
                    UserId = user.Id,
                    UserName = user.FullName ?? "Usuário",
                    IpAddress = $"192.168.1.{random.Next(1, 255)}",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    Result = isSuccess ? "Success" : "Failed",
                    ErrorMessage = isSuccess ? null : "Erro simulado para demonstração",
                    Category = categories[random.Next(categories.Length)],
                    Severity = severities[random.Next(severities.Length)],
                    Details = $"Detalhes da operação {i + 1}"
                });
            }

            context.AuditLogs.AddRange(auditLogs);
        }
        #endregion

        #region Produtividade dos Usuários
        private static async Task SeedUserProductivityAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.UserProductivities.AnyAsync())
            {
                logger.LogDebug("Dados de produtividade já existem, pulando seed...");
                return;
            }

            logger.LogInformation("📈 Criando dados de atividade de usuários (não redundantes com lotes)...");

            var random = new Random();
            var productivity = new List<UserProductivity>();

            foreach (var user in users)
            {
                for (int day = 0; day < 30; day++)
                {
                    var date = DateTime.Today.AddDays(-day);
                    var firstLogin = date.AddHours(8 + random.Next(4)); // Login entre 8h e 12h
                    var lastActivity = firstLogin.AddHours(random.Next(2, 8)); // 2-8h de trabalho

                    productivity.Add(new UserProductivity
                    {
                        UserId = user.Id,
                        Date = date,
                        LoginCount = random.Next(1, 4), // 1-3 logins por dia
                        TotalTimeOnline = TimeSpan.FromHours(random.Next(2, 8)), // 2-8h online
                        PagesAccessed = random.Next(10, 50), // Páginas navegadas
                        FirstLogin = firstLogin,
                        LastActivity = lastActivity
                    });
                }
            }

            context.UserProductivities.AddRange(productivity);
        }
        #endregion

        #region Sessões Ativas
        private static async Task SeedActiveUserSessionsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.ActiveUserSessions.AnyAsync())
            {
                logger.LogDebug("Sessões ativas já existem, pulando seed...");
                return;
            }

            logger.LogInformation("🔄 Criando sessões ativas...");

            var random = new Random();
            var pages = new[] { "/Dashboard", "/Classificar", "/Relatorios", "/Configuracoes", "/Usuario" };
            var activeSessions = new List<ActiveUserSession>();

            // Criar algumas sessões ativas
            for (int i = 0; i < Math.Min(3, users.Count); i++)
            {
                var user = users[i];
                activeSessions.Add(new ActiveUserSession
                {
                    UserId = user.Id,
                    UserName = user.FullName ?? user.UserName ?? "Usuário",
                    SessionId = Guid.NewGuid().ToString(),
                    LoginTime = DateTime.UtcNow.AddMinutes(-random.Next(60, 480)),
                    LastActivity = DateTime.UtcNow.AddMinutes(-random.Next(1, 15)),
                    IpAddress = $"192.168.1.{random.Next(1, 255)}",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    CurrentPage = pages[random.Next(pages.Length)],
                    IsActive = true,
                    Role = user.Department ?? "Geral",
                    Department = user.Department ?? "Sem Departamento"
                });
            }

            context.ActiveUserSessions.AddRange(activeSessions);
        }
        #endregion

        #region Metadados de Data Mining
        private static async Task SeedDataMiningMetadataAsync(ApplicationDbContext context, ILogger logger)
        {
            if (await context.DataMiningMetadata.AnyAsync())
            {
                logger.LogDebug("Metadados já existem, pulando seed...");
                return;
            }

            logger.LogInformation("🔍 Criando metadados de data mining...");

            var metadata = new[]
            {
                new DataMiningMetadata { EntityName = "DocumentProcessingHistory", PropertyName = "DocumentType", DataType = "string", Description = "Tipo do documento classificado", Category = "Classification" },
                new DataMiningMetadata { EntityName = "DocumentProcessingHistory", PropertyName = "Confidence", DataType = "double", Description = "Nível de confiança da classificação", Category = "Metrics" },
                new DataMiningMetadata { EntityName = "UserProductivity", PropertyName = "DocumentsProcessed", DataType = "int", Description = "Quantidade de documentos processados", Category = "Productivity" },
                new DataMiningMetadata { EntityName = "AuditLog", PropertyName = "Action", DataType = "string", Description = "Ação realizada pelo usuário", Category = "Security" },
                new DataMiningMetadata { EntityName = "BatchProcessingHistory", PropertyName = "ProcessingMethod", DataType = "string", Description = "Método de processamento utilizado", Category = "Processing" }
            };

            context.DataMiningMetadata.AddRange(metadata);
        }
        #endregion

        #region Dados de Série Temporal
        private static async Task SeedTimeSeriesDataAsync(ApplicationDbContext context, ILogger logger)
        {
            if (await context.TimeSeriesData.AnyAsync())
            {
                logger.LogDebug("Dados de série temporal já existem, pulando seed...");
                return;
            }

            logger.LogInformation("📊 Criando dados de série temporal...");

            var random = new Random();
            var timeSeriesData = new List<TimeSeriesData>();

            // Dados diários dos últimos 30 dias
            for (int day = 0; day < 30; day++)
            {
                var timestamp = DateTime.Today.AddDays(-day);

                timeSeriesData.AddRange(new[]
                {
                    new TimeSeriesData
                    {
                        SeriesName = "DocumentsProcessedDaily",
                        Timestamp = timestamp,
                        Value = random.Next(50, 200),
                        Category = "Productivity",
                        DataSource = "System",
                        Tags = $"{{\"day_of_week\": \"{timestamp.DayOfWeek}\"}}"
                    },
                    new TimeSeriesData
                    {
                        SeriesName = "AverageConfidenceDaily",
                        Timestamp = timestamp,
                        Value = Math.Round(0.8 + random.NextDouble() * 0.2, 3),
                        Category = "Quality",
                        DataSource = "Classification",
                        Tags = $"{{\"total_documents\": {random.Next(50, 200)}}}"
                    },
                    new TimeSeriesData
                    {
                        SeriesName = "ActiveUsersDaily",
                        Timestamp = timestamp,
                        Value = random.Next(5, 15),
                        Category = "Usage",
                        DataSource = "Sessions",
                        Tags = $"{{\"peak_hour\": {random.Next(8, 17)}}}"
                    }
                });
            }

            context.TimeSeriesData.AddRange(timeSeriesData);
        }
        #endregion

        #region Alertas Automatizados
        private static async Task SeedAutomatedAlertsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.AutomatedAlerts.AnyAsync())
            {
                logger.LogDebug("Alertas já existem, pulando seed...");
                return;
            }

            logger.LogInformation("🚨 Criando alertas automatizados...");

            var admin = users.FirstOrDefault(u => u.Email!.Contains("admin"));
            var alerts = new[]
            {
                new AutomatedAlert
                {
                    Name = "Baixa Confiança na Classificação",
                    Description = "Alerta quando a confiança média cai abaixo de 70%",
                    AlertType = "Quality",
                    Priority = "High",
                    IsActive = true,
                    Condition = "AverageConfidence < 0.7",
                    Recipients = "admin@sistema.com,supervisor@sistema.com",
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    CreatedBy = admin?.Id ?? users.First().Id,
                    LastTriggered = DateTime.UtcNow.AddDays(-2)
                },
                new AutomatedAlert
                {
                    Name = "Alto Volume de Erros",
                    Description = "Alerta quando há mais de 10% de falhas no processamento",
                    AlertType = "Error",
                    Priority = "Critical",
                    IsActive = true,
                    Condition = "ErrorRate > 0.1",
                    Recipients = "admin@sistema.com,ti@sistema.com",
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    CreatedBy = admin?.Id ?? users.First().Id
                },
                new AutomatedAlert
                {
                    Name = "Usuário Inativo",
                    Description = "Alerta quando usuário não processa documentos há 7 dias",
                    AlertType = "Activity",
                    Priority = "Medium",
                    IsActive = true,
                    Condition = "DaysSinceLastActivity > 7",
                    Recipients = "usuarios@sistema.com",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    CreatedBy = admin?.Id ?? users.First().Id
                }
            };

            context.AutomatedAlerts.AddRange(alerts);
        }
        #endregion

        #region Widgets do Dashboard
        private static async Task SeedDashboardWidgetsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.DashboardWidgets.AnyAsync())
            {
                logger.LogDebug("Widgets já existem, pulando seed...");
                return;
            }

            logger.LogInformation("📊 Criando widgets do dashboard...");

            var admin = users.FirstOrDefault(u => u.Email!.Contains("admin"));
            var widgets = new[]
            {
                new DashboardWidget
                {
                    Name = "DocumentsProcessedToday",
                    Title = "Documentos Processados Hoje",
                    WidgetType = "Counter",
                    DataSource = "DocumentProcessingHistory",
                    Configuration = "{\"aggregation\": \"count\", \"filter\": \"today\"}",
                    OrderIndex = 1,
                    IsVisible = true,
                    UserRole = "All",
                    CreatedAt = DateTime.UtcNow.AddDays(-20),
                    CreatedBy = admin?.Id ?? users.First().Id
                },
                new DashboardWidget
                {
                    Name = "AverageConfidenceChart",
                    Title = "Confiança Média - Últimos 7 Dias",
                    WidgetType = "LineChart",
                    DataSource = "TimeSeriesData",
                    Configuration = "{\"series\": \"AverageConfidenceDaily\", \"period\": \"7days\"}",
                    OrderIndex = 2,
                    IsVisible = true,
                    UserRole = "Manager,Admin",
                    CreatedAt = DateTime.UtcNow.AddDays(-18),
                    CreatedBy = admin?.Id ?? users.First().Id
                },
                new DashboardWidget
                {
                    Name = "DocumentTypeDistribution",
                    Title = "Distribuição por Tipo de Documento",
                    WidgetType = "PieChart",
                    DataSource = "DocumentProcessingHistory",
                    Configuration = "{\"groupBy\": \"DocumentType\", \"period\": \"30days\"}",
                    OrderIndex = 3,
                    IsVisible = true,
                    UserRole = "All",
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    CreatedBy = admin?.Id ?? users.First().Id
                }
            };

            context.DashboardWidgets.AddRange(widgets);
        }
        #endregion

        #region Conformidade LGPD
        private static async Task SeedLGPDComplianceAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.LGPDCompliances.AnyAsync())
            {
                logger.LogDebug("Dados de LGPD já existem, pulando seed...");
                return;
            }

            logger.LogInformation("🔒 Criando registros de conformidade LGPD...");

            var random = new Random();
            var actions = new[] { "Collect", "Process", "Store", "Access", "Delete" };
            var dataTypes = new[] { "PersonalData", "SensitiveData", "DocumentMetadata", "UserActivity" };
            var legalBases = new[] { "Consent", "LegitimateInterest", "Contract", "LegalObligation" };

            var lgpdRecords = new List<LGPDCompliance>();

            foreach (var user in users)
            {
                for (int i = 0; i < random.Next(5, 15); i++)
                {
                    lgpdRecords.Add(new LGPDCompliance
                    {
                        UserId = user.Id,
                        DataType = dataTypes[random.Next(dataTypes.Length)],
                        Action = actions[random.Next(actions.Length)],
                        Timestamp = DateTime.UtcNow.AddDays(-random.Next(30)),
                        LegalBasis = legalBases[random.Next(legalBases.Length)],
                        Purpose = "Classificação de documentos de trânsito",
                        Description = $"Processamento de dados para classificação automática",
                        ConsentGiven = random.Next(2) == 1,
                        RetentionUntil = DateTime.UtcNow.AddDays(random.Next(365, 2555)), // 1-7 anos
                        ProcessorInfo = "ClassificationSystem"
                    });
                }
            }

            context.LGPDCompliances.AddRange(lgpdRecords);
        }
        #endregion

        #region Exportações de Dados
        private static async Task SeedDataExportsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.DataExports.AnyAsync())
            {
                logger.LogDebug("Exportações já existem, pulando seed...");
                return;
            }

            logger.LogInformation("📤 Criando histórico de exportações...");

            var random = new Random();
            var formats = new[] { "CSV", "Excel", "PDF", "JSON" };
            var dataTypes = new[] { "ProcessingHistory", "UserProductivity", "AuditLogs", "Reports" };
            var statuses = new[] { "Completed", "Failed", "Processing" };

            var exports = new List<DataExport>();

            foreach (var user in users.Take(4)) // Apenas alguns usuários fizeram exportações
            {
                for (int i = 0; i < random.Next(2, 6); i++)
                {
                    var status = statuses[random.Next(statuses.Length)];
                    var requestedAt = DateTime.UtcNow.AddDays(-random.Next(15));

                    exports.Add(new DataExport
                    {
                        ExportName = $"Relatório_{dataTypes[random.Next(dataTypes.Length)]}_{requestedAt:yyyyMMdd}",
                        Format = formats[random.Next(formats.Length)],
                        DataType = dataTypes[random.Next(dataTypes.Length)],
                        RequestedAt = requestedAt,
                        CompletedAt = status == "Completed" ? requestedAt.AddMinutes(random.Next(5, 30)) : null,
                        UserId = user.Id,
                        Status = status,
                        FilePath = status == "Completed" ? $"/exports/{Guid.NewGuid()}.{formats[random.Next(formats.Length)].ToLower()}" : null,
                        FileSizeBytes = status == "Completed" ? random.Next(10000, 5000000) : null,
                        RecordCount = random.Next(100, 10000)
                    });
                }
            }

            context.DataExports.AddRange(exports);
        }
        #endregion

        #region Notificações do Sistema
        private static async Task SeedSystemNotificationsAsync(ApplicationDbContext context, List<ApplicationUser> users, ILogger logger)
        {
            if (await context.SystemNotifications.AnyAsync())
            {
                logger.LogDebug("Notificações já existem, pulando seed...");
                return;
            }

            logger.LogInformation("🔔 Criando notificações do sistema...");

            var random = new Random();
            var types = new[] { "Info", "Warning", "Error", "Success" };
            var categories = new[] { "System", "User", "Processing", "Security" };

            var notifications = new List<SystemNotification>();

            // Notificações globais
            notifications.AddRange(new[]
            {
                new SystemNotification
                {
                    Title = "Sistema Atualizado",
                    Message = "O sistema foi atualizado com melhorias na classificação visual de documentos.",
                    Type = "INFO",
                    Priority = "NORMAL",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    IsRead = true
                },
                new SystemNotification
                {
                    Title = "Manutenção Programada",
                    Message = "Manutenção programada para este domingo às 02:00. O sistema ficará indisponível por 2 horas.",
                    Type = "WARNING",
                    Priority = "HIGH",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    ExpiresAt = DateTime.UtcNow.AddDays(5)
                }
            });

            // Notificações específicas para usuários
            foreach (var user in users.Take(3))
            {
                notifications.AddRange(new[]
                {
                    new SystemNotification
                    {
                        Title = "Bem-vindo ao Sistema",
                        Message = $"Olá {user.FullName}, bem-vindo ao sistema de classificação de documentos!",
                        Type = "INFO",
                        Priority = "NORMAL",
                        CreatedAt = DateTime.UtcNow.AddDays(-random.Next(10, 20)),
                        UserId = user.Id,
                        IsRead = random.Next(2) == 1
                    },
                    new SystemNotification
                    {
                        Title = "Relatório Mensal Disponível",
                        Message = "Seu relatório mensal de produtividade está disponível para download.",
                        Type = "SUCCESS",
                        Priority = "NORMAL",
                        CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 7)),
                        UserId = user.Id,
                        ActionUrl = "/relatorios/mensal"
                    }
                });
            }

            context.SystemNotifications.AddRange(notifications);
        }
        #endregion
    }
}
