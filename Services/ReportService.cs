using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using FastReport;
using FastReport.Export.PdfSimple;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Serviço para geração de relatórios em PDF/Excel usando FastReport
    /// Atende aos requisitos de exportação e relatórios profissionais
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportService> _logger;
        private readonly string _reportsPath;

        public ReportService(ApplicationDbContext context, ILogger<ReportService> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _reportsPath = Path.Combine(environment.ContentRootPath, "Reports", "Templates");

            // Criar diretório de templates se não existir
            if (!Directory.Exists(_reportsPath))
            {
                Directory.CreateDirectory(_reportsPath);
            }
        }

   
        /// <summary>
        /// Criar DataTable a partir dos dados combinados de produtividade
       
        

      

    

        public async Task<dynamic> ObterDadosRelatorioAsync(string tipoRelatorio, DateTime? dataInicio, DateTime? dataFim, Dictionary<string, string>? filtrosAdicionais = null)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            return tipoRelatorio.ToLower() switch
            {
                "auditoria" => await ObterDadosAuditoriaAsync(inicio, fim),
                "produtividade" => await ObterDadosProdutividadeAsync(inicio, fim),
                "classificacao" => await ObterDadosClassificacaoAsync(inicio, fim,
                    filtrosAdicionais?.GetValueOrDefault("categoria"),
                    filtrosAdicionais?.GetValueOrDefault("status")),
                "lotes" => await ObterDadosLotesAsync(inicio, fim, filtrosAdicionais?.GetValueOrDefault("status")),
                "consolidado" => await ObterDadosConsolidadoAsync(inicio, fim),
                "lgpd" => await ObterDadosLgpdAsync(inicio, fim),
                _ => throw new ArgumentException($"Tipo de relatório não suportado: {tipoRelatorio}")
            };
        }

        public bool ValidarTipoRelatorio(string tipoRelatorio)
        {
            var tiposValidos = new[] { "auditoria", "produtividade", "classificacao", "lotes", "consolidado", "lgpd" };
            return tiposValidos.Contains(tipoRelatorio?.ToLower());
        }

        public string ObterTituloRelatorio(string tipoRelatorio)
        {
            return tipoRelatorio?.ToLower() switch
            {
                "auditoria" => "Relatório de Auditoria",
                "produtividade" => "Relatório de Produtividade",
                "classificacao" => "Relatório de Classificação",
                "lotes" => "Relatório de Lotes",
                "consolidado" => "Relatório Consolidado",
                "lgpd" => "Relatório de Conformidade LGPD",
                _ => "Relatório"
            };
        }

        public async Task<IEnumerable<dynamic>> ObterDadosAuditoriaAsync(DateTime? dataInicio, DateTime? dataFim)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            var logs = await _context.AuditLogs
                .Where(a => a.Timestamp >= inicio && a.Timestamp <= fim)
                .OrderByDescending(a => a.Timestamp)
                .Take(1000)
                .Select(a => new
                {
                    a.Id,
                    DataHora = a.Timestamp,
                    Usuario = a.UserName ?? "Sistema",
                    TipoAcao = a.Action ?? "N/A",
                    NomeDocumento = a.Resource ?? "N/A",
                    Detalhes = a.Details ?? "N/A",
                    EnderecoIP = a.IpAddress ?? "N/A"
                })
                .ToListAsync();

            return logs.Cast<dynamic>();
        }

        public async Task<IEnumerable<dynamic>> ObterDadosProdutividadeAsync(DateTime? dataInicio, DateTime? dataFim)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            try
            {
                // Buscar dados de produtividade (atividade na plataforma) - trazer para memória primeiro
                var produtividadeRaw = await _context.UserProductivities
                    .Where(up => up.Date >= inicio && up.Date <= fim)
                    .ToListAsync();

                // Agrupar em memória para evitar problemas de tradução SQL
                var produtividadeUsuarios = produtividadeRaw
                    .GroupBy(up => up.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        TotalLogins = g.Sum(up => up.LoginCount),
                        TempoTotalOnline = g.Sum(up => up.TotalTimeOnline.TotalMinutes),
                        PaginasAcessadas = g.Sum(up => up.PagesAccessed),
                        PrimeiroLogin = g.Min(up => up.FirstLogin),
                        UltimaAtividade = g.Max(up => up.LastActivity),
                        DiasAtivos = g.Count()
                    })
                    .ToList();

                // Buscar dados de processamento de lotes - trazer para memória primeiro
                var processamentoRaw = await _context.BatchProcessingHistories
                    .Where(bph => bph.StartedAt >= inicio && bph.StartedAt <= fim)
                    .ToListAsync();

                // Agrupar em memória para evitar problemas de tradução SQL
                var processamentoLotes = processamentoRaw
                    .GroupBy(bph => bph.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        UserName = g.First().UserName,
                        TotalLotes = g.Count(),
                        TotalDocumentosProcessados = g.Sum(bph => bph.TotalDocuments),
                        DocumentosComSucesso = g.Sum(bph => bph.SuccessfulDocuments),
                        DocumentosComErro = g.Sum(bph => bph.FailedDocuments),
                        TempoTotalProcessamento = g.Sum(bph => bph.ProcessingDuration?.TotalMinutes ?? 0),
                        ConfiancaMedia = g.Average(bph => bph.AverageConfidence),
                        UltimoProcessamento = g.Max(bph => bph.StartedAt)
                    })
                    .ToList();

                // Buscar nomes dos usuários
                var userIds = processamentoLotes.Select(p => p.UserId)
                    .Union(produtividadeUsuarios.Select(p => p.UserId))
                    .Distinct()
                    .ToList();

                var usuarios = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? "N/A");

                // Combinar dados de produtividade e processamento
                var dadosCombinados = from proc in processamentoLotes
                                      join prod in produtividadeUsuarios on proc.UserId equals prod.UserId into prodJoin
                                      from prod in prodJoin.DefaultIfEmpty()
                                      select new
                                      {
                                          UserId = proc.UserId,
                                          Usuario = usuarios.ContainsKey(proc.UserId) ? usuarios[proc.UserId] : proc.UserName,

                                          // Dados de processamento de documentos
                                          TotalLotes = proc.TotalLotes,
                                          TotalDocumentosProcessados = proc.TotalDocumentosProcessados,
                                          DocumentosComSucesso = proc.DocumentosComSucesso,
                                          DocumentosComErro = proc.DocumentosComErro,
                                          TaxaSucesso = proc.TotalDocumentosProcessados > 0
                                              ? Math.Round((double)proc.DocumentosComSucesso / proc.TotalDocumentosProcessados * 100, 1)
                                              : 0,
                                          ConfiancaMedia = Math.Round(proc.ConfiancaMedia, 1),
                                          TempoMedioProcessamento = proc.TotalLotes > 0
                                              ? TimeSpan.FromMinutes(proc.TempoTotalProcessamento / proc.TotalLotes).ToString(@"mm\:ss")
                                              : "N/A",
                                          UltimoProcessamento = (DateTime?)proc.UltimoProcessamento,

                                          // Dados de atividade na plataforma
                                          TotalLogins = prod?.TotalLogins ?? 0,
                                          TempoTotalOnline = prod != null
                                              ? TimeSpan.FromMinutes(prod.TempoTotalOnline).ToString(@"hh\:mm")
                                              : "N/A",
                                          PaginasAcessadas = prod?.PaginasAcessadas ?? 0,
                                          DiasAtivos = prod?.DiasAtivos ?? 0,
                                          UltimaAtividade = (DateTime?)(prod?.UltimaAtividade ?? proc.UltimoProcessamento)
                                      };

                // Adicionar usuários que só têm atividade na plataforma (sem processamento)
                var usuariosSemProcessamento = produtividadeUsuarios
                    .Where(prod => !processamentoLotes.Any(proc => proc.UserId == prod.UserId))
                    .Select(prod => new
                    {
                        UserId = prod.UserId,
                        Usuario = usuarios.ContainsKey(prod.UserId) ? usuarios[prod.UserId] : "N/A",

                        // Dados de processamento (zero)
                        TotalLotes = 0,
                        TotalDocumentosProcessados = 0,
                        DocumentosComSucesso = 0,
                        DocumentosComErro = 0,
                        TaxaSucesso = 0.0,
                        ConfiancaMedia = 0.0,
                        TempoMedioProcessamento = "N/A",
                        UltimoProcessamento = (DateTime?)null,

                        // Dados de atividade na plataforma
                        TotalLogins = prod.TotalLogins,
                        TempoTotalOnline = TimeSpan.FromMinutes(prod.TempoTotalOnline).ToString(@"hh\:mm"),
                        PaginasAcessadas = prod.PaginasAcessadas,
                        DiasAtivos = prod.DiasAtivos,
                        UltimaAtividade = (DateTime?)prod.UltimaAtividade
                    });

                var resultadoFinal = dadosCombinados
                    .Concat(usuariosSemProcessamento)
                    .OrderByDescending(d => d.TotalDocumentosProcessados)
                    .ThenByDescending(d => d.TotalLogins)
                    .ToList();

                return resultadoFinal.Cast<dynamic>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de produtividade do período {Inicio} a {Fim}", inicio, fim);

                // Fallback para dados básicos em caso de erro
                return new List<dynamic>
                {
                    new {
                        Usuario = "Erro",
                        TotalDocumentosProcessados = 0,
                        TaxaSucesso = 0.0,
                        TempoMedioProcessamento = "N/A",
                        Erro = ex.Message
                    }
                };
            }
        }

        public Task<IEnumerable<dynamic>> ObterDadosClassificacaoAsync(DateTime? dataInicio, DateTime? dataFim, string? categoria = null, string? status = null)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            // Simulação de dados de classificação
            var dados = new List<dynamic>
            {
                new { Categoria = "Contratos", Total = 45, Sucesso = 42, Erro = 2, Pendente = 1, TaxaSucesso = 93.3 },
                new { Categoria = "Faturas", Total = 78, Sucesso = 75, Erro = 1, Pendente = 2, TaxaSucesso = 96.2 },
                new { Categoria = "Certidões", Total = 32, Sucesso = 30, Erro = 1, Pendente = 1, TaxaSucesso = 93.8 },
                new { Categoria = "Outros", Total = 25, Sucesso = 22, Erro = 2, Pendente = 1, TaxaSucesso = 88.0 }
            };

            return Task.FromResult(dados.Where(d => categoria == null || d.Categoria == categoria).AsEnumerable());
        }

        public Task<IEnumerable<dynamic>> ObterDadosLotesAsync(DateTime? dataInicio, DateTime? dataFim, string? status = null)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            // Simulação de dados de lotes
            var dados = new List<dynamic>
            {
                new { IdLote = "LOTE001", DataCriacao = DateTime.Now.AddDays(-2), TotalDocumentos = 50, Processados = 50, Status = "Concluído", Progresso = 100, Usuario = "Admin" },
                new { IdLote = "LOTE002", DataCriacao = DateTime.Now.AddDays(-1), TotalDocumentos = 30, Processados = 25, Status = "Em Processamento", Progresso = 83, Usuario = "Operador1" },
                new { IdLote = "LOTE003", DataCriacao = DateTime.Now, TotalDocumentos = 40, Processados = 10, Status = "Em Processamento", Progresso = 25, Usuario = "Operador2" }
            };

            return Task.FromResult(dados.Where(d => status == null || d.Status == status).AsEnumerable());
        }

        public Task<dynamic> ObterDadosConsolidadoAsync(DateTime? dataInicio, DateTime? dataFim)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            var dados = new
            {
                TotalDocumentos = 180,
                TotalClassificados = 169,
                TotalProcessando = 8,
                TotalErros = 3,
                TaxaSucessoGeral = 93.9,
                TempoMedioProcessamento = "3m 12s",
                DocumentosPorDia = 6.0
            };

            return Task.FromResult((dynamic)dados);
        }

        public Task<IEnumerable<dynamic>> ObterDadosLgpdAsync(DateTime? dataInicio, DateTime? dataFim)
        {
            var inicio = dataInicio ?? DateTime.Now.AddDays(-30);
            var fim = dataFim ?? DateTime.Now;

            // Simulação de dados LGPD
            var dados = new List<dynamic>
            {
                new { ItemConformidade = "Política de Privacidade", Status = "Conforme", UltimaVerificacao = DateTime.Now.AddDays(-5), Observacoes = "Política atualizada e publicada" },
                new { ItemConformidade = "Consentimento de Dados", Status = "Conforme", UltimaVerificacao = DateTime.Now.AddDays(-3), Observacoes = "Sistema de consentimento implementado" },
                new { ItemConformidade = "Direito ao Esquecimento", Status = "Parcialmente Conforme", UltimaVerificacao = DateTime.Now.AddDays(-7), Observacoes = "Processo manual, necessita automação" },
                new { ItemConformidade = "Portabilidade de Dados", Status = "Conforme", UltimaVerificacao = DateTime.Now.AddDays(-2), Observacoes = "API de exportação implementada" },
                new { ItemConformidade = "Relatório de Impacto", Status = "Conforme", UltimaVerificacao = DateTime.Now.AddDays(-10), Observacoes = "Relatório elaborado e aprovado" }
            };

            return Task.FromResult(dados.AsEnumerable());
        }

       
    }
}
