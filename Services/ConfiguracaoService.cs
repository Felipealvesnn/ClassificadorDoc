using ClassificadorDoc.Data;
using ClassificadorDoc.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Serviço para gerenciar configurações do sistema
    /// </summary>
    public interface IConfiguracaoService
    {
        Task<string?> ObterValorAsync(string chave);
        Task<T?> ObterValorAsync<T>(string chave, T? valorPadrao = default);
        Task DefinirValorAsync(string chave, string valor, string? descricao = null, string categoria = "Geral", string? usuarioId = null);
        Task<bool> ChaveExisteAsync(string chave);
        Task<List<Configuracao>> ObterPorCategoriaAsync(string categoria);
        Task<ConfiguracaoViewModel> ObterConfiguracoesSalvamentoAsync();
        Task AtualizarConfiguracoesSalvamentoAsync(ConfiguracaoViewModel configuracao, string? usuarioId = null);
    }

    public class ConfiguracaoService : IConfiguracaoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ConfiguracaoService> _logger;

        public ConfiguracaoService(ApplicationDbContext context, ILogger<ConfiguracaoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string?> ObterValorAsync(string chave)
        {
            try
            {
                var configuracao = await _context.Configuracoes
                    .Where(c => c.Chave == chave && c.Ativo)
                    .FirstOrDefaultAsync();

                return configuracao?.Valor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter configuração {Chave}", chave);
                return null;
            }
        }

        public async Task<T?> ObterValorAsync<T>(string chave, T? valorPadrao = default)
        {
            try
            {
                var valor = await ObterValorAsync(chave);

                if (string.IsNullOrEmpty(valor))
                    return valorPadrao;

                // Conversão para o tipo desejado
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)bool.Parse(valor);
                }
                else if (typeof(T) == typeof(int))
                {
                    return (T)(object)int.Parse(valor);
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)valor;
                }

                return valorPadrao;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao converter configuração {Chave} para tipo {Tipo}", chave, typeof(T).Name);
                return valorPadrao;
            }
        }

        public async Task DefinirValorAsync(string chave, string valor, string? descricao = null, string categoria = "Geral", string? usuarioId = null)
        {
            try
            {
                var configuracao = await _context.Configuracoes
                    .Where(c => c.Chave == chave)
                    .FirstOrDefaultAsync();

                if (configuracao == null)
                {
                    // Criar nova configuração
                    configuracao = new Configuracao
                    {
                        Chave = chave,
                        Valor = valor,
                        Descricao = descricao,
                        Categoria = categoria,
                        DataCriacao = DateTime.UtcNow,
                        DataAtualizacao = DateTime.UtcNow,
                        UsuarioAtualizacao = usuarioId,
                        Ativo = true
                    };

                    _context.Configuracoes.Add(configuracao);
                }
                else
                {
                    // Atualizar configuração existente
                    configuracao.Valor = valor;
                    configuracao.DataAtualizacao = DateTime.UtcNow;
                    configuracao.UsuarioAtualizacao = usuarioId;

                    if (!string.IsNullOrEmpty(descricao))
                        configuracao.Descricao = descricao;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Configuração {Chave} atualizada com sucesso", chave);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar configuração {Chave}", chave);
                throw;
            }
        }

        public async Task<bool> ChaveExisteAsync(string chave)
        {
            return await _context.Configuracoes
                .AnyAsync(c => c.Chave == chave && c.Ativo);
        }

        public async Task<List<Configuracao>> ObterPorCategoriaAsync(string categoria)
        {
            return await _context.Configuracoes
                .Where(c => c.Categoria == categoria && c.Ativo)
                .OrderBy(c => c.Chave)
                .ToListAsync();
        }

        public async Task<ConfiguracaoViewModel> ObterConfiguracoesSalvamentoAsync()
        {
            try
            {
                var caminhoSalvamento = await ObterValorAsync(ChavesConfiguracao.CAMINHO_SALVAMENTO_DOCUMENTOS);
                var diretorioBase = await ObterValorAsync(ChavesConfiguracao.DIRETORIO_BASE_DOCUMENTOS) ?? "DocumentosProcessados";
                var nomePastaClassificador = await ObterValorAsync(ChavesConfiguracao.NOME_PASTA_CLASSIFICADOR) ?? "ClassificadorDoc";
                var estruturaPastasHabilitada = await ObterValorAsync<bool>(ChavesConfiguracao.ESTRUTURA_PASTAS_HABILITADA, true);

                return new ConfiguracaoViewModel
                {
                    CaminhoSalvamento = caminhoSalvamento ?? string.Empty,
                    DiretorioBase = diretorioBase,
                    NomePastaClassificador = nomePastaClassificador,
                    EstruturaPastasHabilitada = estruturaPastasHabilitada
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter configurações de salvamento");

                // Retornar configurações padrão em caso de erro
                return new ConfiguracaoViewModel
                {
                    CaminhoSalvamento = string.Empty,
                    DiretorioBase = "DocumentosProcessados",
                    NomePastaClassificador = "ClassificadorDoc",
                    EstruturaPastasHabilitada = true
                };
            }
        }

        public async Task AtualizarConfiguracoesSalvamentoAsync(ConfiguracaoViewModel configuracao, string? usuarioId = null)
        {
            try
            {
                await DefinirValorAsync(
                    ChavesConfiguracao.CAMINHO_SALVAMENTO_DOCUMENTOS,
                    configuracao.CaminhoSalvamento,
                    "Caminho personalizado onde os documentos processados serão salvos",
                    "Salvamento",
                    usuarioId);

                await DefinirValorAsync(
                    ChavesConfiguracao.DIRETORIO_BASE_DOCUMENTOS,
                    configuracao.DiretorioBase,
                    "Nome do diretório base para documentos processados",
                    "Salvamento",
                    usuarioId);

                await DefinirValorAsync(
                    ChavesConfiguracao.NOME_PASTA_CLASSIFICADOR,
                    configuracao.NomePastaClassificador,
                    "Nome da pasta principal do ClassificadorDoc",
                    "Salvamento",
                    usuarioId);

                await DefinirValorAsync(
                    ChavesConfiguracao.ESTRUTURA_PASTAS_HABILITADA,
                    configuracao.EstruturaPastasHabilitada.ToString(),
                    "Habilita/desabilita a criação de estrutura de pastas organizadas",
                    "Salvamento",
                    usuarioId);

                _logger.LogInformation("Configurações de salvamento atualizadas com sucesso pelo usuário {UsuarioId}", usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar configurações de salvamento");
                throw;
            }
        }
    }
}
