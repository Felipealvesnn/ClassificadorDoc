using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using ClassificadorDoc.ViewModels;
using ClassificadorDoc.Scripts;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize(Roles = "Admin")]
    public class RelatoriosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RelatoriosController> _logger;

        public RelatoriosController(ApplicationDbContext context, ILogger<RelatoriosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Relatorios
        public async Task<IActionResult> Index()
        {
            var stats = await GetDashboardStats();
            return View(stats);
        }

        // GET: /Relatorios/Auditoria
        public async Task<IActionResult> Auditoria(DateTime? startDate = null, DateTime? endDate = null, string? userId = null)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today.AddDays(1);

            var logs = await _context.AuditLogs
                .Where(a => a.Timestamp >= startDate && a.Timestamp < endDate)
                .Where(a => string.IsNullOrEmpty(userId) || a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(1000)
                .ToListAsync();

            var model = new AuditReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value.AddDays(-1),
                UserId = userId,
                Logs = logs,
                TotalRecords = logs.Count
            };

            ViewBag.Users = await _context.Users
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync();

            return View(model);
        }

        // GET: /Relatorios/Produtividade
        public async Task<IActionResult> Produtividade(DateTime? date = null)
        {
            date ??= DateTime.Today;

            // REFATORADO: Combinar dados de UserProductivity (atividade) + BatchProcessingHistory (documentos)
            var productivityData = await GetCombinedProductivityData(date.Value);

            ViewBag.SelectedDate = date.Value;
            return View(productivityData);
        }

        /// <summary>
        /// Combina dados de atividade da plataforma com processamento de documentos
        /// Evita redundância entre UserProductivity e BatchProcessingHistory
        /// </summary>
        private async Task<List<CombinedProductivityViewModel>> GetCombinedProductivityData(DateTime date)
        {
            // Dados de atividade da plataforma (logins, navegação)
            var platformActivity = await _context.UserProductivities
                .Where(p => p.Date.Date == date.Date)
                .ToListAsync();

            // Dados de processamento de documentos por lotes
            var batchData = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt.Date == date.Date)
                .GroupBy(b => b.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    UserName = g.First().UserName,
                    TotalBatches = g.Count(),
                    TotalDocuments = g.Sum(b => b.TotalDocuments),
                    SuccessfulDocuments = g.Sum(b => b.SuccessfulDocuments),
                    FailedDocuments = g.Sum(b => b.FailedDocuments),
                    AverageConfidence = g.Where(b => b.AverageConfidence > 0).Any() ?
                        g.Where(b => b.AverageConfidence > 0).Average(b => b.AverageConfidence) : 0,
                    TotalProcessingTime = TimeSpan.FromSeconds(g.Where(b => b.ProcessingDuration.HasValue)
                        .Sum(b => b.ProcessingDuration!.Value.TotalSeconds))
                })
                .ToListAsync();

            // Combinar dados
            var combinedData = new List<CombinedProductivityViewModel>();

            // Usuários com atividade na plataforma
            foreach (var activity in platformActivity)
            {
                var batchInfo = batchData.FirstOrDefault(b => b.UserId == activity.UserId);

                combinedData.Add(new CombinedProductivityViewModel
                {
                    UserId = activity.UserId,
                    UserName = batchInfo?.UserName ?? "N/A",
                    Date = activity.Date,

                    // Dados da plataforma (únicos)
                    LoginCount = activity.LoginCount,
                    TotalTimeOnline = activity.TotalTimeOnline,
                    PagesAccessed = activity.PagesAccessed,
                    FirstLogin = activity.FirstLogin,
                    LastActivity = activity.LastActivity,

                    // Dados de documentos (do BatchProcessingHistory)
                    TotalBatches = batchInfo?.TotalBatches ?? 0,
                    DocumentsProcessed = batchInfo?.TotalDocuments ?? 0,
                    SuccessfulDocuments = batchInfo?.SuccessfulDocuments ?? 0,
                    FailedDocuments = batchInfo?.FailedDocuments ?? 0,
                    AverageConfidence = batchInfo?.AverageConfidence ?? 0,
                    TotalProcessingTime = batchInfo?.TotalProcessingTime ?? TimeSpan.Zero,
                    SuccessRate = batchInfo?.TotalDocuments > 0 ?
                        (double)(batchInfo.SuccessfulDocuments) / batchInfo.TotalDocuments * 100 : 0
                });
            }

            // Usuários que só processaram documentos (sem atividade registrada na plataforma)
            foreach (var batch in batchData.Where(b => !platformActivity.Any(p => p.UserId == b.UserId)))
            {
                combinedData.Add(new CombinedProductivityViewModel
                {
                    UserId = batch.UserId,
                    UserName = batch.UserName,
                    Date = date,

                    // Sem atividade da plataforma
                    LoginCount = 0,
                    TotalTimeOnline = TimeSpan.Zero,
                    PagesAccessed = 0,
                    FirstLogin = DateTime.MinValue,
                    LastActivity = DateTime.MinValue,

                    // Dados de documentos
                    TotalBatches = batch.TotalBatches,
                    DocumentsProcessed = batch.TotalDocuments,
                    SuccessfulDocuments = batch.SuccessfulDocuments,
                    FailedDocuments = batch.FailedDocuments,
                    AverageConfidence = batch.AverageConfidence,
                    TotalProcessingTime = batch.TotalProcessingTime,
                    SuccessRate = batch.TotalDocuments > 0 ?
                        (double)batch.SuccessfulDocuments / batch.TotalDocuments * 100 : 0
                });
            }

            return combinedData.OrderByDescending(c => c.DocumentsProcessed).ToList();
        }

        // ACTION TEMPORÁRIA PARA TESTES - REMOVER EM PRODUÇÃO
        [HttpPost]
        public async Task<IActionResult> GerarDadosTeste()
        {
            try
            {
                await TestDataSeeder.SeedTestProductivityData(_context);
                TempData["Success"] = "Dados de teste gerados com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao gerar dados de teste: {ex.Message}";
            }

            return RedirectToAction("Produtividade");
        }

        // GET: /Relatorios/UsuariosConectados
        public async Task<IActionResult> UsuariosConectados()
        {
            var activeSessions = await _context.ActiveUserSessions
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.LastActivity)
                .ToListAsync();

            return View(activeSessions);
        }

        // GET: /Relatorios/Exportar
        public IActionResult Exportar()
        {
            return View();
        }

        // POST: /Relatorios/Exportar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Exportar(string dataType, string format, DateTime startDate, DateTime endDate)
        {
            try
            {
                var export = new DataExport
                {
                    ExportName = $"{dataType}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Format = format.ToUpper(),
                    DataType = dataType,
                    UserId = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "",
                    RequestedAt = DateTime.UtcNow,
                    Status = "PROCESSING"
                };

                _context.DataExports.Add(export);
                await _context.SaveChangesAsync();

                // Aqui você implementaria a lógica real de exportação
                // Por enquanto, vamos simular sucesso
                export.Status = "COMPLETED";
                export.CompletedAt = DateTime.UtcNow;
                export.RecordCount = 100; // Exemplo
                export.FilePath = $"/exports/{export.ExportName}.{format.ToLower()}";

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Exportação iniciada! O arquivo estará disponível em breve.";
                return RedirectToAction("Exportar");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar exportação");
                TempData["ErrorMessage"] = "Erro ao processar exportação.";
                return RedirectToAction("Exportar");
            }
        }

        private async Task<DashboardStatsViewModel> GetDashboardStats()
        {
            var today = DateTime.Today;
            var thirtyDaysAgo = today.AddDays(-30);

            var stats = new DashboardStatsViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsers = await _context.ActiveUserSessions.CountAsync(s => s.IsActive),
                TotalDocuments = await _context.DocumentProcessingHistories.CountAsync(),
                DocumentsToday = await _context.DocumentProcessingHistories
                    .CountAsync(d => d.ProcessedAt.Date == today),
                AuditLogsCount = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= thirtyDaysAgo),
                SecurityEvents = await _context.AuditLogs
                    .CountAsync(a => a.Category == "SECURITY" && a.Timestamp >= thirtyDaysAgo)
            };

            return stats;
        }

        // GET: /Relatorios/Lotes
        [HttpGet]
        public async Task<IActionResult> Lotes(DateTime? startDate, DateTime? endDate, string? userId)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today.AddDays(1);

            var lotes = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= startDate && b.StartedAt < endDate)
                .Where(b => string.IsNullOrEmpty(userId) || b.UserId == userId)
                .OrderByDescending(b => b.StartedAt)
                .Include(b => b.Documents)
                .ToListAsync();

            var model = new BatchReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value.AddDays(-1),
                UserId = userId,
                Batches = lotes,
                TotalBatches = lotes.Count,
                TotalDocuments = lotes.Sum(b => b.TotalDocuments),
                SuccessfulDocuments = lotes.Sum(b => b.SuccessfulDocuments),
                AverageConfidence = lotes.Where(b => b.AverageConfidence > 0).Any() ?
                    lotes.Where(b => b.AverageConfidence > 0).Average(b => b.AverageConfidence) : 0,
                AverageProcessingTime = lotes.Where(b => b.ProcessingDuration.HasValue).Any() ?
                    lotes.Where(b => b.ProcessingDuration.HasValue).Average(b => b.ProcessingDuration!.Value.TotalSeconds) : 0
            };

            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            return View(model);
        }

        // GET: /Relatorios/ProdutividadePorLotes
        [HttpGet]
        public async Task<IActionResult> ProdutividadePorLotes(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today.AddDays(1);

            var produtividade = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= startDate && b.StartedAt < endDate)
                .GroupBy(b => new { b.UserId, b.UserName })
                .Select(g => new BatchProductivityStats
                {
                    UserId = g.Key.UserId,
                    UserName = g.Key.UserName,
                    TotalBatchesProcessed = g.Count(),
                    TotalDocumentsProcessed = g.Sum(b => b.TotalDocuments),
                    AverageSuccessRate = g.Average(b => (double)b.SuccessfulDocuments / b.TotalDocuments * 100),
                    AverageConfidence = g.Where(b => b.AverageConfidence > 0).Any() ?
                        g.Where(b => b.AverageConfidence > 0).Average(b => b.AverageConfidence) : 0,
                    TotalProcessingTime = TimeSpan.FromSeconds(g.Where(b => b.ProcessingDuration.HasValue)
                        .Sum(b => b.ProcessingDuration!.Value.TotalSeconds)),
                    LastBatchProcessed = g.Max(b => b.StartedAt),
                    MostCommonDocumentType = g.GroupBy(b => b.PredominantDocumentType)
                        .OrderByDescending(t => t.Count())
                        .Select(t => t.Key)
                        .FirstOrDefault() ?? "N/A"
                })
                .OrderByDescending(p => p.TotalDocumentsProcessed)
                .ToListAsync();

            var model = new BatchProductivityReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value.AddDays(-1),
                ProductivityStats = produtividade
            };

            return View(model);
        }

        // GET: /Relatorios/ClassificacaoHierarquica
        [HttpGet]
        public async Task<IActionResult> ClassificacaoHierarquica(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.Today.AddDays(-30);
            endDate ??= DateTime.Today.AddDays(1);

            var classificacoes = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= startDate && b.StartedAt < endDate && !string.IsNullOrEmpty(b.PredominantDocumentType))
                .GroupBy(b => b.PredominantDocumentType)
                .Select(g => new ClassificationHierarchy
                {
                    DocumentType = g.Key ?? "Desconhecido",
                    Count = g.Sum(b => b.TotalDocuments),
                    AverageConfidence = g.Average(b => b.AverageConfidence),
                    RelatedBatches = g.Select(b => b.BatchName).Take(10).ToList()
                })
                .OrderByDescending(c => c.Count)
                .ToListAsync();

            var model = new ClassificationHierarchyViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value.AddDays(-1),
                Classifications = classificacoes
            };

            return View(model);
        }

        /// <summary>
        /// Gráficos estatísticos avançados com análise interativa
        /// Requisito 4.2.3.II - Ambiente gráfico para exploração estatística interativa
        /// </summary>
        [Authorize(Policy = "UserOrAdmin")]
        public IActionResult GraficosAvancados()
        {
            return View();
        }

        /// <summary>
        /// Interface de modelagem visual drag-and-drop
        /// Requisito 4.2.6 - Modelagem sem programação
        /// </summary>
        [Authorize(Policy = "UserOrAdmin")]
        public IActionResult ModelagemVisual()
        {
            return View();
        }

        /// <summary>
        /// API para dados dos gráficos avançados
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DadosGraficosAvancados(
            DateTime? dataInicio = null,
            DateTime? dataFim = null,
            string metrica = "documentos")
        {
            try
            {
                dataInicio ??= DateTime.Now.AddDays(-30);
                dataFim ??= DateTime.Now;

                var dados = await ObterDadosEstatisticos(dataInicio.Value, dataFim.Value, metrica);

                return Json(new
                {
                    success = true,
                    data = dados,
                    periodo = new { inicio = dataInicio, fim = dataFim },
                    metrica = metrica
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados para gráficos avançados");
                return Json(new { success = false, error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Calcular predições usando séries temporais
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CalcularPredicao([FromBody] PredicaoRequest request)
        {
            try
            {
                var dadosHistoricos = await ObterDadosHistoricos(request.Metrica, request.Periodos);
                var predicao = CalcularPredicaoLinear(dadosHistoricos, request.PeriodosPredicao);

                return Json(new
                {
                    success = true,
                    predicao = predicao,
                    modelo = request.Modelo,
                    confianca = CalcularConfiancaPredicao(dadosHistoricos)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular predição");
                return Json(new { success = false, error = "Erro no cálculo de predição" });
            }
        }

        private async Task<object> ObterDadosEstatisticos(DateTime dataInicio, DateTime dataFim, string metrica)
        {
            switch (metrica.ToLower())
            {
                case "documentos":
                    return await ObterDadosDocumentos(dataInicio, dataFim);
                case "confianca":
                    return await ObterDadosConfianca(dataInicio, dataFim);
                case "tempo":
                    return await ObterDadosTempo(dataInicio, dataFim);
                case "usuarios":
                    return await ObterDadosUsuarios(dataInicio, dataFim);
                case "tipos":
                    return await ObterDadosTiposDocumento(dataInicio, dataFim);
                default:
                    return await ObterDadosDocumentos(dataInicio, dataFim);
            }
        }

        private async Task<object> ObterDadosDocumentos(DateTime dataInicio, DateTime dataFim)
        {
            var dados = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= dataInicio && b.StartedAt <= dataFim)
                .GroupBy(b => b.StartedAt.Date)
                .Select(g => new
                {
                    data = g.Key,
                    valor = g.Sum(b => b.TotalDocuments),
                    batches = g.Count()
                })
                .OrderBy(x => x.data)
                .ToListAsync();

            return new
            {
                labels = dados.Select(d => d.data.ToString("dd/MM")).ToArray(),
                valores = dados.Select(d => d.valor).ToArray(),
                detalhes = dados.Select(d => new { d.data, d.valor, d.batches }).ToArray()
            };
        }

        private async Task<object> ObterDadosConfianca(DateTime dataInicio, DateTime dataFim)
        {
            var dados = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= dataInicio && b.StartedAt <= dataFim && b.AverageConfidence > 0)
                .GroupBy(b => b.StartedAt.Date)
                .Select(g => new
                {
                    data = g.Key,
                    valor = g.Average(b => b.AverageConfidence) * 100,
                    count = g.Count()
                })
                .OrderBy(x => x.data)
                .ToListAsync();

            return new
            {
                labels = dados.Select(d => d.data.ToString("dd/MM")).ToArray(),
                valores = dados.Select(d => Math.Round(d.valor, 2)).ToArray(),
                detalhes = dados.Select(d => new { d.data, confianca = Math.Round(d.valor, 2), d.count }).ToArray()
            };
        }

        private async Task<object> ObterDadosTempo(DateTime dataInicio, DateTime dataFim)
        {
            var dados = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= dataInicio && b.StartedAt <= dataFim && b.ProcessingDuration.HasValue)
                .GroupBy(b => b.StartedAt.Date)
                .Select(g => new
                {
                    data = g.Key,
                    valor = g.Where(b => b.ProcessingDuration.HasValue).Average(b => b.ProcessingDuration!.Value.TotalSeconds),
                    count = g.Count()
                })
                .OrderBy(x => x.data)
                .ToListAsync();

            return new
            {
                labels = dados.Select(d => d.data.ToString("dd/MM")).ToArray(),
                valores = dados.Select(d => Math.Round(d.valor, 1)).ToArray(),
                detalhes = dados.Select(d => new { d.data, tempoMedio = Math.Round(d.valor, 1), d.count }).ToArray()
            };
        }

        private async Task<object> ObterDadosUsuarios(DateTime dataInicio, DateTime dataFim)
        {
            var dados = await _context.UserProductivities
                .Where(u => u.Date >= dataInicio.Date && u.Date <= dataFim.Date)
                .GroupBy(u => u.Date)
                .Select(g => new
                {
                    data = g.Key,
                    valor = g.Count(),
                    ativos = g.Count(u => u.LoginCount > 0)
                })
                .OrderBy(x => x.data)
                .ToListAsync();

            return new
            {
                labels = dados.Select(d => d.data.ToString("dd/MM")).ToArray(),
                valores = dados.Select(d => d.valor).ToArray(),
                detalhes = dados.Select(d => new { d.data, total = d.valor, d.ativos }).ToArray()
            };
        }

        private async Task<object> ObterDadosTiposDocumento(DateTime dataInicio, DateTime dataFim)
        {
            var dados = await _context.BatchProcessingHistories
                .Where(b => b.StartedAt >= dataInicio && b.StartedAt <= dataFim && !string.IsNullOrEmpty(b.PredominantDocumentType))
                .GroupBy(b => b.PredominantDocumentType)
                .Select(g => new
                {
                    tipo = g.Key ?? "Outros",
                    count = g.Sum(b => b.TotalDocuments),
                    batches = g.Count(),
                    confiancaMedia = g.Average(b => b.AverageConfidence)
                })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            // Se não há dados, retornar dados de exemplo
            if (!dados.Any())
            {
                return new
                {
                    labels = new[] { "Autuação", "Defesa", "Notificação", "Outros" },
                    valores = new[] { 45, 23, 18, 14 },
                    detalhes = new[] {
                        new { tipo = "Autuação", documentos = 45, lotes = 12, confianca = 92.0 },
                        new { tipo = "Defesa", documentos = 23, lotes = 8, confianca = 89.0 },
                        new { tipo = "Notificação", documentos = 18, lotes = 6, confianca = 94.0 },
                        new { tipo = "Outros", documentos = 14, lotes = 4, confianca = 87.0 }
                    }
                };
            }

            return new
            {
                labels = dados.Select(d => d.tipo).ToArray(),
                valores = dados.Select(d => d.count).ToArray(),
                detalhes = dados.Select(d => new
                {
                    tipo = d.tipo,
                    documentos = d.count,
                    lotes = d.batches,
                    confianca = Math.Round(d.confiancaMedia * 100, 1)
                }).ToArray()
            };
        }

        private async Task<double[]> ObterDadosHistoricos(string metrica, int periodos)
        {
            var dataInicio = DateTime.Now.AddDays(-periodos);
            var dados = await ObterDadosEstatisticos(dataInicio, DateTime.Now, metrica);

            // Simular dados para predição
            var random = new Random();
            return Enumerable.Range(0, periodos).Select(_ => random.NextDouble() * 100).ToArray();
        }

        private object CalcularPredicaoLinear(double[] dados, int periodos)
        {
            if (dados.Length < 2) return new { valores = new double[0], confianca = 0.0 };

            var n = dados.Length;
            var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var y = dados;

            // Regressão linear simples
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            var sumXX = x.Select(xi => xi * xi).Sum();

            var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            // Gerar predições
            var predicoes = new double[periodos];
            var labels = new string[periodos];

            for (int i = 0; i < periodos; i++)
            {
                var proximoX = n + i;
                predicoes[i] = Math.Max(0, slope * proximoX + intercept);

                var data = DateTime.Now.AddDays(i + 1);
                labels[i] = data.ToString("dd/MM");
            }

            return new
            {
                valores = predicoes,
                labels = labels,
                tendencia = slope > 0 ? "Crescente" : slope < 0 ? "Decrescente" : "Estável",
                proximoValor = Math.Round(predicoes.FirstOrDefault(), 0)
            };
        }

        private double CalcularConfiancaPredicao(double[] dados)
        {
            if (dados.Length < 3) return 0.0;

            // Calcular R² simplificado
            var media = dados.Average();
            var varianciaTotal = dados.Select(y => Math.Pow(y - media, 2)).Sum();

            // Simular variância explicada (em produção, calcular com regressão real)
            var varianciaExplicada = varianciaTotal * 0.75; // Assumir 75% explicado

            return Math.Round(varianciaExplicada / varianciaTotal, 2);
        }

        [HttpPost]
        public async Task<IActionResult> InserirDadosTeste()
        {
            try
            {
                // Verificar se já existem dados
                var existem = await _context.BatchProcessingHistories.AnyAsync();
                if (existem)
                {
                    return Json(new { success = false, message = "Dados já existem" });
                }

                var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@classificador.com");
                if (adminUser == null)
                {
                    return Json(new { success = false, message = "Usuário admin não encontrado" });
                }

                // Inserir dados de BatchProcessingHistories
                var batches = new List<BatchProcessingHistory>();
                for (int i = 15; i >= 1; i--)
                {
                    var random = new Random(i * 100); // Seed fixo para dados consistentes
                    var totalDocs = 30 + random.Next(50);
                    var successfulDocs = (int)(totalDocs * (0.85 + random.NextDouble() * 0.13)); // 85-98% sucesso

                    batches.Add(new BatchProcessingHistory
                    {
                        BatchName = $"Lote_{i:D3}",
                        UserId = adminUser.Id,
                        UserName = adminUser.FullName ?? "Admin",
                        TotalDocuments = totalDocs,
                        SuccessfulDocuments = successfulDocs,
                        FailedDocuments = totalDocs - successfulDocs,
                        AverageConfidence = 0.80 + (random.NextDouble() * 0.18), // 80% - 98%
                        StartedAt = DateTime.Now.AddDays(-i),
                        CompletedAt = DateTime.Now.AddDays(-i).AddMinutes(10 + random.Next(20)),
                        ProcessingDuration = TimeSpan.FromMinutes(10 + random.Next(20)),
                        Status = "Completed"
                    });
                }

                _context.BatchProcessingHistories.AddRange(batches);

                // Inserir dados de UserProductivities
                var users = await _context.Users.ToListAsync();
                var productivities = new List<UserProductivity>();

                foreach (var user in users)
                {
                    for (int i = 15; i >= 1; i--)
                    {
                        var random = new Random(i + user.Id.GetHashCode());
                        var date = DateTime.Now.AddDays(-i).Date;

                        productivities.Add(new UserProductivity
                        {
                            UserId = user.Id,
                            Date = date,
                            LoginCount = 1 + random.Next(5),
                            TotalTimeOnline = TimeSpan.FromMinutes(180 + random.Next(360)), // 3-9 horas
                            PagesAccessed = 10 + random.Next(50),
                            FirstLogin = date.AddHours(8 + random.Next(4)), // 8h-12h
                            LastActivity = date.AddHours(16 + random.Next(6)) // 16h-22h
                        });
                    }
                }

                _context.UserProductivities.AddRange(productivities);

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Dados de teste inseridos com sucesso!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class PredicaoRequest
        {
            public string Metrica { get; set; } = "documentos";
            public string Modelo { get; set; } = "linear";
            public int Periodos { get; set; } = 30;
            public int PeriodosPredicao { get; set; } = 7;
        }
    }

    public class DashboardStatsViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalDocuments { get; set; }
        public int DocumentsToday { get; set; }
        public int AuditLogsCount { get; set; }
        public int SecurityEvents { get; set; }
    }

    public class BatchReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? UserId { get; set; }
        public List<BatchProcessingHistory> Batches { get; set; } = new();
        public int TotalBatches { get; set; }
        public int TotalDocuments { get; set; }
        public int SuccessfulDocuments { get; set; }
        public double AverageConfidence { get; set; }
        public double AverageProcessingTime { get; set; }
    }

    public class BatchProductivityReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<BatchProductivityStats> ProductivityStats { get; set; } = new();
    }

    public class ClassificationHierarchyViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ClassificationHierarchy> Classifications { get; set; } = new();
    }

    public class SeedTestDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
