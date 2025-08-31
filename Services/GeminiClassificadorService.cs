using ClassificadorDoc.Models;
using System.Text.Json;
using System.Text;

namespace ClassificadorDoc.Services
{
    public class GeminiClassificadorService : IClassificadorService
    {
        private readonly string _apiKey;
        private readonly ILogger<GeminiClassificadorService> _logger;
        private readonly HttpClient _httpClient;

        public GeminiClassificadorService(IConfiguration configuration, ILogger<GeminiClassificadorService> logger, HttpClient httpClient)
        {
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API Key não configurada");
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoPdfAsync(string nomeArquivo, byte[] pdfBytes)
        {
            return await ClassificarDocumentoVisualAsync(nomeArquivo, pdfBytes, "application/pdf");
        }



        private async Task<DocumentoClassificacao> ClassificarDocumentoVisualAsync(string nomeArquivo, byte[] arquivoBytes, string mimeType)
        {
            const int maxTentativas = 3;
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                try
                {
                    // Validação básica do arquivo
                    if (arquivoBytes == null || arquivoBytes.Length == 0)
                    {
                        throw new ArgumentException("Arquivo vazio ou inválido");
                    }

                    // Criando o prompt adaptado para o tipo de arquivo
                    var prompt = CriarPromptClassificacaoVisual(mimeType);

                    // Log adicional para PDFs que podem ser escaneados
                    if (mimeType.Contains("pdf"))
                    {
                        _logger.LogInformation("Processando PDF para {NomeArquivo} - pode ser texto nativo ou escaneado", nomeArquivo);
                    }

                    // Chama a API do Gemini via HTTP com dados visuais
                    var textoResposta = await ChamarGeminiApiAsync(prompt, arquivoBytes, mimeType);

                    if (string.IsNullOrEmpty(textoResposta))
                    {
                        var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
                        throw new InvalidOperationException($"Resposta vazia do Gemini para {tipoArquivo}");
                    }

                    // Extrair JSON da resposta que pode vir em bloco markdown
                    var jsonLimpo = ExtrairJsonDaResposta(textoResposta);
                    var classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(jsonLimpo);

                    if (classificacao == null)
                    {
                        var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
                        throw new InvalidOperationException($"Falha ao deserializar resposta do Gemini para {tipoArquivo}");
                    }

                    var tipoAnalise = mimeType.Contains("pdf") ? "PDF (texto nativo ou escaneado)" : "imagem digitalizada";

                    if (tentativa > 1)
                    {
                        _logger.LogInformation("Classificação visual bem-sucedida na tentativa {Tentativa} para {NomeArquivo}",
                            tentativa, nomeArquivo);
                    }

                    return new DocumentoClassificacao
                    {
                        NomeArquivo = nomeArquivo,
                        TipoDocumento = classificacao.tipo_documento,
                        ConfiancaClassificacao = classificacao.confianca,
                        ResumoConteudo = classificacao.resumo,
                        PalavrasChaveEncontradas = classificacao.GetPalavrasChaveComoString(),
                        TextoExtraido = $"[{tipoAnalise} analisado via Gemini API - análise visual]",
                        ProcessadoComSucesso = true
                    };
                }
                catch (Exception ex) when (tentativa < maxTentativas &&
                    (ex.Message.Contains("inner stream position") ||
                     ex.Message.Contains("stream") ||
                     ex.Message.Contains("position") ||
                     ex.Message.Contains("timeout") ||
                     ex.Message.Contains("network") ||
                     ex.Message.Contains("OCR") ||
                     ex.Message.Contains("vision") ||
                     ex.Message.Contains("image processing") ||
                     ex.Message.Contains("concurrency") ||
                     ex.Message.Contains("thread") ||
                     ex.Message.Contains("HTTP") ||
                     ex.Message.Contains("API")))
                {
                    ultimaExcecao = ex;
                    var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
                    _logger.LogWarning("Tentativa {Tentativa} falhou para {TipoArquivo} {NomeArquivo}: {Erro}. Tentando novamente...",
                        tentativa, tipoArquivo, nomeArquivo, ex.Message);

                    // Aguarda antes de tentar novamente com tempo crescente
                    await Task.Delay(TimeSpan.FromSeconds(tentativa * 2));
                    continue;
                }
                catch (Exception ex)
                {
                    ultimaExcecao = ex;
                    break; // Erro não transitório
                }
            }

            var tipoArquivoFinal = mimeType.Contains("pdf") ? "PDF" : "imagem";
            _logger.LogError(ultimaExcecao, "Erro ao classificar {TipoArquivo} {NomeArquivo} com Gemini após {MaxTentativas} tentativas",
                tipoArquivoFinal, nomeArquivo, maxTentativas);

            return new DocumentoClassificacao
            {
                NomeArquivo = nomeArquivo,
                TipoDocumento = "Erro",
                ConfiancaClassificacao = 0,
                ResumoConteudo = $"Erro no processamento do {tipoArquivoFinal}",
                TextoExtraido = string.Empty,
                ProcessadoComSucesso = false,
                ErroProcessamento = ultimaExcecao?.Message ?? "Erro desconhecido após múltiplas tentativas"
            };
        }

        private async Task<string> ChamarGeminiApiAsync(string prompt, byte[]? arquivoBytes = null, string? mimeType = null)
        {
            try
            {
                object requestBody;

                // Se há arquivo visual, inclui na requisição
                if (arquivoBytes != null && !string.IsNullOrEmpty(mimeType))
                {
                    var base64Data = Convert.ToBase64String(arquivoBytes);

                    requestBody = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                parts = new object[]
                                {
                                    new { text = prompt },
                                    new
                                    {
                                        inline_data = new
                                        {
                                            mime_type = mimeType,
                                            data = base64Data
                                        }
                                    }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1,
                            topK = 32,
                            topP = 0.1,
                            maxOutputTokens = 2048
                        }
                    };
                }
                else
                {
                    // Requisição apenas com texto (fallback)
                    requestBody = new
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
                            topK = 32,
                            topP = 0.1,
                            maxOutputTokens = 2048
                        }
                    };
                }

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Erro na API do Gemini: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent);

                if (responseJson.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentElement) &&
                    contentElement.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? string.Empty;
                }

                throw new InvalidOperationException("Resposta da API do Gemini em formato inesperado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao chamar a API do Gemini");
                throw;
            }
        }

        private string ExtrairJsonDaResposta(string resposta)
        {
            if (string.IsNullOrEmpty(resposta))
                return resposta;

            // Remove blocos de código markdown se existirem
            if (resposta.Contains("```json"))
            {
                var inicioJson = resposta.IndexOf("```json") + "```json".Length;
                var fimJson = resposta.IndexOf("```", inicioJson);

                if (fimJson > inicioJson)
                {
                    return resposta.Substring(inicioJson, fimJson - inicioJson).Trim();
                }
            }

            // Se não tiver markdown, procura pelo JSON diretamente
            if (resposta.Contains("{") && resposta.Contains("}"))
            {
                var inicioJson = resposta.IndexOf("{");
                var fimJson = resposta.LastIndexOf("}") + 1;

                if (fimJson > inicioJson)
                {
                    return resposta.Substring(inicioJson, fimJson - inicioJson).Trim();
                }
            }

            // Retorna a resposta original se não conseguir extrair
            return resposta.Trim();
        }

        private string CriarPromptClassificacaoVisual(string mimeType)
        {
            var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
            var instrucaoEspecifica = mimeType.Contains("pdf")
                ? "Use a capacidade visual do Gemini para examinar este documento PDF (que pode ser texto nativo ou escaneado):"
                : "Use a capacidade visual do Gemini para examinar esta imagem escaneada de documento:";

            return $@"
Analise este documento de trânsito brasileiro completamente. {instrucaoEspecifica}

ATENÇÃO: Este {tipoArquivo} pode conter MÚLTIPLOS documentos. Identifique o DOCUMENTO PRINCIPAL/PRIMÁRIO baseado em:
- Qual documento ocupa mais espaço/páginas
- Qual é o propósito principal do arquivo
- Se há um documento que claramente é o foco (ex: defesa com anexos)

IMPORTANTE: Este {tipoArquivo} pode conter:
- Texto nativo (PDF digital com texto selecionável)
- Imagens escaneadas (PDF de digitalização com OCR necessário)
- Fotos de documentos com qualidade variável
- Documentos inclinados, com sombras ou reflexos

TIPOS DE DOCUMENTO DE TRÂNSITO (ANALISE NA ORDEM PARA MELHOR PRECISÃO):

1. AUTUACAO: Auto de Infração de Trânsito (AIT) - documento ORIGINAL da infração
   INDICADORES OBRIGATÓRIOS:
   - Título AUTO DE INFRAÇÃO ou AIT
   - Dados do agente autuador (matrícula, nome)
   - Local, data e hora EXATOS da infração
   - Descrição da irregularidade observada pelo agente
   - Código da infração CTB
   - Assinatura/identificação do agente fiscalizador

2. NOTIFICACAO_AUTUACAO: Comunicado oficial sobre a infração (não é cobrança)
   INDICADORES OBRIGATÓRIOS:
   - Título NOTIFICAÇÃO DE AUTUAÇÃO
   - Texto formal informando sobre a lavratura do AIT
   - Instruções para defesa (prazos, documentos necessários)
   - Formulário FICI (identificação de condutor)
   - AUSÊNCIA de valores para pagamento ou códigos de barras
   - Texto: tem finalidade de cientificá-lo da autuação, não tem efeito para pagamento

3. NOTIFICACAO_PENALIDADE: Cobrança oficial da multa (após processo)
   INDICADORES OBRIGATÓRIOS:
   - Título NOTIFICAÇÃO DE PENALIDADE ou NIP
   - Valores definidos para pagamento da multa
   - Códigos de barras ou dados bancários
   - Prazos para pagamento com desconto
   - Confirmação final da multa após análise

4. DEFESA: Documento onde proprietário/condutor CONTESTA a infração
   INDICADORES OBRIGATÓRIOS:
   - Texto com DEFESA, REQUERIMENTO DE DEFESA, RECURSO
   - Argumentação jurídica contestando a infração
   - Cabeçalho dirigido à autoridade (Ilustríssimo Senhor...)
   - Texto argumentativo explicando por que a multa deve ser cancelada
   - Assinatura do requerente (proprietário/condutor)
   - Pedidos explícitos de cancelamento/arquivamento

5. OUTROS: Demais documentos relacionados

PALAVRAS-CHAVE ESPECÍFICAS POR TIPO:

AUTUACAO (AIT original):
- AUTO DE INFRAÇÃO, AIT
- Matrícula do agente, dados do fiscalizador
- lavrado por, autuado

NOTIFICACAO_AUTUACAO:
- NOTIFICAÇÃO DE AUTUAÇÃO
- cientificá-lo da autuação
- não tem efeito para pagamento
- aguarde a notificação de penalidade
- FICI (formulário identificação condutor)

NOTIFICACAO_PENALIDADE:
- NOTIFICAÇÃO DE PENALIDADE, NIP
- Valores monetários definidos
- pagamento, quitação
- Códigos de barras

DEFESA:
- DEFESA, REQUERIMENTO DE DEFESA, RECURSO
- requer, alega, contesta, impugna
- Ilustríssimo, Vossa Excelência
- Argumentação jurídica contestando

ESTRATÉGIA DE ANÁLISE SEQUENCIAL:
1. Procure primeiro pelo TÍTULO principal do documento
2. Identifique a FINALIDADE: informar, cobrar ou contestar?
3. Verifique indicadores específicos de cada tipo
4. Se há múltiplos documentos, identifique qual é o PRINCIPAL
5. ATENÇÃO ESPECIAL:
   - NOTIFICAÇÃO DE AUTUAÇÃO ≠ DEFESA (mesmo que mencione como fazer defesa)
   - NOTIFICAÇÃO DE AUTUAÇÃO ≠ COBRANÇA (apenas informa sobre a infração)
   - DEFESA sempre tem argumentação contestando, não apenas formulários
6. Confirme com as palavras-chave específicas

REGRA CRÍTICA DE DECISÃO:
- Se o documento INFORMA sobre uma infração = NOTIFICACAO_AUTUACAO
- Se o documento REGISTRA uma infração = AUTUACAO
- Se o documento COBRA uma multa = NOTIFICACAO_PENALIDADE
- Se o documento CONTESTA uma infração = DEFESA

Retorne APENAS este JSON (sem blocos de código markdown):
{{
    ""tipo_documento"": ""[autuacao|notificacao_autuacao|notificacao_penalidade|defesa|outros]"",
    ""confianca"": [0.0-1.0],
    ""resumo"": ""Análise do documento principal identificado, mencionando se há documentos anexos"",
    ""palavras_chave_encontradas"": ""Elementos encontrados separados por vírgula"",
    ""documentos_identificados"": ""Lista dos tipos de documento encontrados no arquivo""
}}
";
        }
        private class ClassificacaoResposta
        {
            public string tipo_documento { get; set; } = string.Empty;
            public double confianca { get; set; }
            public string resumo { get; set; } = string.Empty;

            // Aceita tanto string quanto array de strings
            private object? _palavras_chave_encontradas;

            public object? palavras_chave_encontradas
            {
                get => _palavras_chave_encontradas;
                set => _palavras_chave_encontradas = value;
            }

            // Método helper para obter como string
            public string GetPalavrasChaveComoString()
            {
                if (_palavras_chave_encontradas == null)
                    return string.Empty;

                if (_palavras_chave_encontradas is string str)
                    return str;

                if (_palavras_chave_encontradas is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                        return element.GetString() ?? string.Empty;

                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        var items = element.EnumerateArray()
                            .Select(x => x.GetString() ?? string.Empty)
                            .Where(x => !string.IsNullOrEmpty(x));
                        return string.Join(", ", items);
                    }
                }

                return _palavras_chave_encontradas.ToString() ?? string.Empty;
            }
        }
    }
}
