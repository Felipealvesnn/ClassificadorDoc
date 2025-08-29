using ClassificadorDoc.Models;
using System.Text.Json;
using System.Text;

namespace ClassificadorDoc.Services
{
    public class GeminiClassificadorService : IClassificadorService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiClassificadorService> _logger;

        public GeminiClassificadorService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiClassificadorService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API Key não configurada");
            _logger = logger;
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoAsync(string nomeArquivo, string textoDocumento)
        {
            try
            {
                var prompt = CriarPromptClassificacao(textoDocumento);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        maxOutputTokens = 500,
                        responseMimeType = "application/json"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro na API do Gemini: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new InvalidOperationException($"Erro na API do Gemini: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent);

                if (geminiResponse?.candidates == null || geminiResponse.candidates.Length == 0)
                {
                    throw new InvalidOperationException("Resposta inválida do Gemini");
                }

                var textoResposta = geminiResponse.candidates[0].content.parts[0].text;
                var classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(textoResposta);

                if (classificacao == null)
                {
                    throw new InvalidOperationException("Falha ao deserializar resposta do Gemini");
                }

                return new DocumentoClassificacao
                {
                    NomeArquivo = nomeArquivo,
                    TipoDocumento = classificacao.tipo_documento,
                    ConfiancaClassificacao = classificacao.confianca,
                    ResumoConteudo = classificacao.resumo,
                    PalavrasChaveEncontradas = classificacao.palavras_chave_encontradas,
                    TextoExtraido = textoDocumento,
                    ProcessadoComSucesso = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao classificar documento {NomeArquivo} com Gemini", nomeArquivo);
                return new DocumentoClassificacao
                {
                    NomeArquivo = nomeArquivo,
                    TipoDocumento = "Erro",
                    ConfiancaClassificacao = 0,
                    ResumoConteudo = "Erro no processamento",
                    TextoExtraido = textoDocumento ?? string.Empty,
                    ProcessadoComSucesso = false,
                    ErroProcessamento = ex.Message
                };
            }
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoPdfAsync(string nomeArquivo, byte[] pdfBytes)
        {
            try
            {
                var base64Pdf = Convert.ToBase64String(pdfBytes);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = CriarPromptClassificacaoPdf() },
                                new {
                                    inline_data = new {
                                        mime_type = "application/pdf",
                                        data = base64Pdf
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        maxOutputTokens = 500,
                        responseMimeType = "application/json"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro na API do Gemini para PDF: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new InvalidOperationException($"Erro na API do Gemini: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent);

                if (geminiResponse?.candidates == null || geminiResponse.candidates.Length == 0)
                {
                    throw new InvalidOperationException("Resposta inválida do Gemini para PDF");
                }

                var textoResposta = geminiResponse.candidates[0].content.parts[0].text;
                var classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(textoResposta);

                if (classificacao == null)
                {
                    throw new InvalidOperationException("Falha ao deserializar resposta do Gemini para PDF");
                }

                return new DocumentoClassificacao
                {
                    NomeArquivo = nomeArquivo,
                    TipoDocumento = classificacao.tipo_documento,
                    ConfiancaClassificacao = classificacao.confianca,
                    ResumoConteudo = classificacao.resumo,
                    PalavrasChaveEncontradas = classificacao.palavras_chave_encontradas,
                    TextoExtraido = "[PDF analisado diretamente pelo Gemini - análise visual completa]",
                    ProcessadoComSucesso = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao classificar PDF {NomeArquivo} com Gemini", nomeArquivo);
                return new DocumentoClassificacao
                {
                    NomeArquivo = nomeArquivo,
                    TipoDocumento = "Erro",
                    ConfiancaClassificacao = 0,
                    ResumoConteudo = "Erro no processamento do PDF",
                    TextoExtraido = string.Empty,
                    ProcessadoComSucesso = false,
                    ErroProcessamento = ex.Message
                };
            }
        }

        private string CriarPromptClassificacao(string textoDocumento)
        {
            // Limita o texto para evitar exceder limites de tokens
            var textoLimitado = textoDocumento.Length > 4000
                ? textoDocumento.Substring(0, 4000) + "..."
                : textoDocumento;

            return $@"
Você é um especialista em análise de documentos de trânsito brasileiros. Analise o CONTEÚDO ESPECÍFICO preenchido no documento abaixo e classifique-o baseado no que está escrito.

INSTRUÇÕES IMPORTANTES:
- Foque no CONTEÚDO preenchido, não apenas no formato do documento
- Um mesmo formulário pode ser usado para diferentes finalidades de trânsito
- Procure por palavras-chave específicas e contexto da infração/defesa
- Considere códigos de infração (CTB), valores de multa, dados do veículo

TIPOS DE DOCUMENTO DE TRÂNSITO:
- autuacao: Auto de Infração de Trânsito (AIT), Notificação de Autuação, aplicação de multa, constatação de infração
- defesa: Defesa prévia, Recurso JARI (1ª instância), Recurso CETRAN (2ª instância), Indicação de condutor, contestação de multa
- notificacao_penalidade: Notificação da Penalidade (NIP), intimação para pagamento, comunicação de multa aplicada
- outros: Documentos de trânsito que não se encaixam nas categorias acima

PALAVRAS-CHAVE PARA IDENTIFICAÇÃO:
AUTUACAO: ""auto de infração"", ""AIT"", ""autuação"", ""infração constatada"", ""código CTB"", ""agente de trânsito"", ""lavrado"", ""multa aplicada""
DEFESA: ""defesa"", ""recurso"", ""JARI"", ""CETRAN"", ""contestação"", ""impugnação"", ""indicação de condutor"", ""alegações"", ""discordância""
NOTIFICACAO: ""notificação da penalidade"", ""NIP"", ""intimação"", ""prazo para pagamento"", ""valor da multa"", ""fica notificado"", ""débito""

DOCUMENTO PARA ANÁLISE:
{textoLimitado}

Analise cuidadosamente o conteúdo preenchido e retorne APENAS um JSON válido no seguinte formato exato:
{{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
    ""confianca"": 0.95,
    ""resumo"": ""Descreva o que foi identificado no documento de trânsito em até 200 caracteres"",
    ""palavras_chave_encontradas"": ""Principais palavras ou códigos que justificaram a classificação""
}}
";
        }

        private string CriarPromptClassificacaoPdf()
        {
            return @"
Analise este documento PDF de trânsito brasileiro completamente. Use a capacidade visual do Gemini para examinar:

ANÁLISE VISUAL COMPLETA:
- Layout e formatação do documento
- Carimbos, assinaturas, logos oficiais
- Campos preenchidos vs vazios
- Elementos gráficos que indiquem o tipo
- Qualidade (original, cópia, digitalização)

TIPOS DE DOCUMENTO DE TRÂNSITO:
- autuacao: Auto de Infração de Trânsito (AIT), Notificação de Autuação - documento que registra a infração
- defesa: Defesa de Autuação, Recurso JARI/CETRAN, Defesa Prévia, Indicação de Condutor - documentos de contestação
- notificacao_penalidade: Notificação da Penalidade (NIP), Intimação para pagamento - confirmação oficial da multa
- outros: Outros documentos relacionados ao trânsito

INDICADORES VISUAIS IMPORTANTES:
AUTUAÇÃO: Campos do agente autuador, local/data da infração, código CTB, descrição da irregularidade
DEFESA: Texto argumentativo, pedidos, alegações, assinatura do proprietário/condutor
NOTIFICAÇÃO: Valores da multa, dados para pagamento, prazos, confirmação da penalidade

ELEMENTOS A OBSERVAR:
- Cabeçalho com nome do órgão (DETRAN, PRF, etc.)
- Presença de campos específicos preenchidos
- Linguagem formal vs argumentativa
- Elementos que confirmem a finalidade do documento

Retorne APENAS este JSON:
{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
    ""confianca"": 0.95,
    ""resumo"": ""Descrição baseada na análise visual e textual completa do PDF"",
    ""palavras_chave_encontradas"": ""Elementos visuais e textuais encontrados que justificaram a classificação""
}
";
        }

        private class ClassificacaoResposta
        {
            public string tipo_documento { get; set; } = string.Empty;
            public double confianca { get; set; }
            public string resumo { get; set; } = string.Empty;
            public string palavras_chave_encontradas { get; set; } = string.Empty;
        }

        private class GeminiResponse
        {
            public Candidate[] candidates { get; set; } = Array.Empty<Candidate>();
        }

        private class Candidate
        {
            public Content content { get; set; } = new Content();
        }

        private class Content
        {
            public Part[] parts { get; set; } = Array.Empty<Part>();
        }

        private class Part
        {
            public string text { get; set; } = string.Empty;
        }
    }
}
