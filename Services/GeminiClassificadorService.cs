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
            // Usar apenas análise visual da IA para extrair tudo (texto + classificação)
            var resultado = await ClassificarDocumentoVisualAsync(nomeArquivo, pdfBytes, "application/pdf");
            return resultado;
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

                    // Log adicional para PDFs que podem ser escaneados
                    if (mimeType.Contains("pdf"))
                    {
                        _logger.LogInformation("Processando PDF para {NomeArquivo} - pode ser texto nativo ou escaneado", nomeArquivo);
                    }

                    // NOVA ESTRATÉGIA: Duas chamadas separadas para evitar limite de tokens

                    // PRIMEIRA CHAMADA: Classificação + dados específicos (sem texto completo)
                    var promptClassificacao = CriarPromptClassificacaoSemTexto(mimeType);
                    var respostaClassificacao = await ChamarGeminiApiAsync(promptClassificacao, arquivoBytes, mimeType);

                    if (string.IsNullOrEmpty(respostaClassificacao))
                    {
                        var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
                        throw new InvalidOperationException($"Resposta vazia do Gemini para classificação do {tipoArquivo}");
                    }

                    _logger.LogDebug("🔍 Resposta de classificação do Gemini: {Resposta}", respostaClassificacao);
                    var jsonClassificacao = ExtrairJsonDaResposta(respostaClassificacao);
                    _logger.LogDebug("🧹 JSON de classificação extraído: {JsonLimpo}", jsonClassificacao);

                    ClassificacaoResposta? classificacao;
                    try
                    {
                        classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(jsonClassificacao);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "❌ Erro ao deserializar JSON de classificação. JSON: {Json}", jsonClassificacao);
                        throw new InvalidOperationException($"JSON de classificação inválido recebido do Gemini: {ex.Message}. JSON: {jsonClassificacao}");
                    }

                    if (classificacao == null)
                    {
                        var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
                        throw new InvalidOperationException($"Falha ao deserializar resposta de classificação do Gemini para {tipoArquivo}");
                    }

                    // SEGUNDA CHAMADA: Extração completa do texto
                    _logger.LogInformation("📄 Iniciando extração completa do texto para {NomeArquivo}", nomeArquivo);
                    var promptTexto = CriarPromptExtracao(mimeType);
                    var respostaTexto = await ChamarGeminiApiAsync(promptTexto, arquivoBytes, mimeType);

                    string textoCompleto = string.Empty;
                    if (!string.IsNullOrEmpty(respostaTexto))
                    {
                        textoCompleto = LimparTextoExtraido(respostaTexto);
                        _logger.LogDebug("📝 Texto extraído com {Tamanho} caracteres", textoCompleto.Length);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Não foi possível extrair texto completo do documento");
                        textoCompleto = $"[Erro na extração de texto do {(mimeType.Contains("pdf") ? "PDF" : "imagem")}]";
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
                        TextoExtraido = textoCompleto, // Agora vem da segunda chamada
                        ProcessadoComSucesso = true,

                        // CAMPOS ESPECÍFICOS EXTRAÍDOS
                        NumeroAIT = classificacao.numero_ait,
                        PlacaVeiculo = classificacao.placa_veiculo,
                        NomeCondutor = classificacao.nome_condutor,
                        NumeroCNH = classificacao.numero_cnh,
                        TextoDefesa = classificacao.texto_defesa,
                        DataInfracao = TentarConverterData(classificacao.data_infracao),
                        LocalInfracao = classificacao.local_infracao,
                        CodigoInfracao = classificacao.codigo_infracao,
                        ValorMulta = TentarConverterValor(classificacao.valor_multa),
                        OrgaoAutuador = classificacao.orgao_autuador,

                        // NOVOS CAMPOS PARA INDICAÇÃO DE CONDUTOR
                        RequerenteNome = classificacao.requerente_nome,
                        RequerenteCPF = classificacao.requerente_cpf,
                        RequerenteRG = classificacao.requerente_rg,
                        RequerenteEndereco = classificacao.requerente_endereco,
                        IndicacaoNome = classificacao.indicacao_nome,
                        IndicacaoCPF = classificacao.indicacao_cpf,
                        IndicacaoRG = classificacao.indicacao_rg,
                        IndicacaoCNH = classificacao.indicacao_cnh
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
                            maxOutputTokens = 4096 // Aumentado para suportar texto completo
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
                            maxOutputTokens = 4096
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

            // Log para debug
            _logger.LogDebug("🔍 Extraindo JSON da resposta: {PrimeirosCaracteres}...",
                resposta.Length > 100 ? resposta.Substring(0, 100) : resposta);

            // Remove caracteres problemáticos no início
            resposta = resposta.Trim().TrimStart('`').TrimEnd('`').Trim();

            // Remove blocos de código markdown se existirem
            if (resposta.Contains("```json"))
            {
                var inicioJson = resposta.IndexOf("```json") + "```json".Length;
                var fimJson = resposta.IndexOf("```", inicioJson);

                if (fimJson > inicioJson)
                {
                    var jsonExtraido = resposta.Substring(inicioJson, fimJson - inicioJson).Trim();
                    _logger.LogDebug("✅ JSON extraído de bloco markdown: {Json}", jsonExtraido);
                    return jsonExtraido;
                }
            }

            // Remove blocos de código simples com ```
            if (resposta.StartsWith("```") && resposta.EndsWith("```"))
            {
                var semBlocos = resposta.Substring(3, resposta.Length - 6).Trim();
                if (semBlocos.StartsWith("{") && semBlocos.EndsWith("}"))
                {
                    _logger.LogDebug("✅ JSON extraído de bloco simples: {Json}", semBlocos);
                    return semBlocos;
                }
            }

            // Se não tiver markdown, procura pelo JSON diretamente
            if (resposta.Contains("{") && resposta.Contains("}"))
            {
                var inicioJson = resposta.IndexOf("{");
                var fimJson = resposta.LastIndexOf("}") + 1;

                if (fimJson > inicioJson)
                {
                    var jsonExtraido = resposta.Substring(inicioJson, fimJson - inicioJson).Trim();
                    _logger.LogDebug("✅ JSON extraído diretamente: {Json}", jsonExtraido);
                    return jsonExtraido;
                }
            }

            // Retorna a resposta original se não conseguir extrair
            _logger.LogWarning("⚠️ Não foi possível extrair JSON, retornando resposta original");
            return resposta.Trim();
        }

        private string CriarPromptClassificacaoSemTexto(string mimeType)
        {
            var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
            var instrucaoEspecifica = mimeType.Contains("pdf")
                ? "Use a capacidade visual do Gemini para examinar este documento PDF (que pode ser texto nativo ou escaneado):"
                : "Use a capacidade visual do Gemini para examinar esta imagem escaneada de documento:";

            return $@"
Analise este documento de trânsito brasileiro para CLASSIFICAÇÃO e EXTRAÇÃO DE DADOS ESPECÍFICOS. {instrucaoEspecifica}

IMPORTANTE: NÃO inclua o texto completo nesta resposta - apenas classifique e extraia dados específicos.

ATENÇÃO: Este {tipoArquivo} pode conter MÚLTIPLOS documentos. Identifique o DOCUMENTO PRINCIPAL/PRIMÁRIO baseado em:
- Qual documento ocupa mais espaço/páginas
- Qual é o propósito principal do arquivo
- Se há um documento que claramente é o foco (ex: defesa com anexos)

TIPOS DE DOCUMENTO DE TRÂNSITO (ANALISE NA ORDEM PARA MELHOR PRECISÃO):

1. AUTUACAO: Auto de Infração de Trânsito (AIT) - documento ORIGINAL da infração
2. NOTIFICACAO_AUTUACAO: Comunicado oficial sobre a infração (não é cobrança)
3. NOTIFICACAO_PENALIDADE: Cobrança oficial da multa (após processo)
4. DEFESA: Documento onde proprietário/condutor CONTESTA a infração
5. INDICACAO_CONDUTOR: Formulário para indicar quem era o condutor no momento da infração
6. OUTROS: Demais documentos relacionados

EXTRAÇÃO DE DADOS ESPECÍFICOS:
- numero_ait: Número do AIT/Auto de Infração
- placa_veiculo: Placa do veículo (formato AAA-1234 ou AAA1A23)
- nome_condutor: Nome completo do condutor (para indicação de condutor)
- numero_cnh: Número da CNH (para indicação de condutor)
- texto_defesa: Texto da argumentação da defesa (para defesas)
- data_infracao: Data da infração (DD/MM/AAAA)
- local_infracao: Local onde ocorreu a infração
- codigo_infracao: Código CTB da infração
- valor_multa: Valor da multa em reais
- orgao_autuador: Órgão que aplicou a multa

DADOS ESPECÍFICOS PARA INDICAÇÃO DE CONDUTOR:
**DADOS DO REQUERENTE (proprietário do veículo):**
- requerente_nome: Nome completo do requerente/proprietário
- requerente_cpf: CPF do requerente (formato 000.000.000-00)
- requerente_rg: RG/documento do requerente
- requerente_endereco: Endereço completo do requerente

**DADOS DA INDICAÇÃO (condutor no momento da infração):**
- indicacao_nome: Nome completo do condutor indicado
- indicacao_cpf: CPF do condutor indicado (formato 000.000.000-00)
- indicacao_rg: RG/documento do condutor indicado
- indicacao_cnh: CNH do condutor indicado

Retorne APENAS este JSON (sem blocos de código markdown):
{{
    ""tipo_documento"": ""[autuacao|notificacao_autuacao|notificacao_penalidade|defesa|indicacao_condutor|outros]"",
    ""confianca"": [0.0-1.0],
    ""resumo"": ""Análise do documento principal identificado, mencionando se há documentos anexos"",
    ""palavras_chave_encontradas"": ""Elementos encontrados separados por vírgula"",
    ""numero_ait"": ""Número do AIT encontrado ou null"",
    ""placa_veiculo"": ""Placa do veículo encontrada ou null"",
    ""nome_condutor"": ""Nome do condutor ou null"",
    ""numero_cnh"": ""Número da CNH ou null"",
    ""texto_defesa"": ""Texto da defesa ou null"",
    ""data_infracao"": ""Data da infração em formato DD/MM/AAAA ou null"",
    ""local_infracao"": ""Local da infração ou null"",
    ""codigo_infracao"": ""Código CTB da infração ou null"",
    ""valor_multa"": ""Valor da multa em reais (apenas números) ou null"",
    ""orgao_autuador"": ""Órgão que aplicou a multa ou null"",
    ""requerente_nome"": ""Nome do requerente/proprietário ou null"",
    ""requerente_cpf"": ""CPF do requerente ou null"",
    ""requerente_rg"": ""RG do requerente ou null"",
    ""requerente_endereco"": ""Endereço do requerente ou null"",
    ""indicacao_nome"": ""Nome do condutor indicado ou null"",
    ""indicacao_cpf"": ""CPF do condutor indicado ou null"",
    ""indicacao_rg"": ""RG do condutor indicado ou null"",
    ""indicacao_cnh"": ""CNH do condutor indicado ou null""
}}
";
        }

        private string CriarPromptExtracao(string mimeType)
        {
            var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
            var instrucaoEspecifica = mimeType.Contains("pdf")
                ? "Use a capacidade visual do Gemini para examinar este documento PDF (que pode ser texto nativo ou escaneado):"
                : "Use a capacidade visual do Gemini para examinar esta imagem escaneada de documento:";

            return $@"
EXTRAÇÃO COMPLETA DE TEXTO - {instrucaoEspecifica}

OBJETIVO: Extrair TODO O TEXTO visível neste documento, incluindo:
- Texto nativo do PDF (selecionável)  
- Texto em imagens escaneadas (usando OCR/análise visual)
- Texto manuscrito legível
- Qualquer texto visível no documento

INSTRUÇÕES:
1. Extraia TODO o texto do documento, página por página se necessário
2. Mantenha a formatação e estrutura quando possível
3. Se há múltiplos documentos, extraia o texto de TODOS
4. Inclua cabeçalhos, rodapés, assinaturas, carimbos legíveis
5. Se algum texto estiver cortado ou ilegível, indique com [ILEGÍVEL]

IMPORTANTE: 
- NÃO faça análise ou classificação
- NÃO resuma o conteúdo  
- APENAS extraia o texto completo
- Se o documento é muito longo, priorize completude sobre formatação

Retorne apenas o texto extraído, sem formatação JSON ou markdown.
";
        }

        private string LimparTextoExtraido(string textoResposta)
        {
            if (string.IsNullOrEmpty(textoResposta))
                return string.Empty;

            // Remove possíveis blocos de código markdown
            var texto = textoResposta.Trim();

            if (texto.StartsWith("```") && texto.EndsWith("```"))
            {
                // Remove blocos de código
                var linhas = texto.Split('\n');
                if (linhas.Length > 2)
                {
                    // Remove primeira e última linha se forem marcadores de código
                    var primeiraLinha = linhas[0].Trim();
                    var ultimaLinha = linhas[^1].Trim();

                    if (primeiraLinha.StartsWith("```") && ultimaLinha == "```")
                    {
                        texto = string.Join('\n', linhas[1..^1]);
                    }
                }
            }

            // Remove instruções que o Gemini pode ter adicionado
            var linhasInstrucoes = new[]
            {
                "aqui está o texto extraído",
                "texto extraído do documento:",
                "conteúdo do documento:",
                "texto completo:",
                "segue o texto:"
            };

            foreach (var instrucao in linhasInstrucoes)
            {
                if (texto.StartsWith(instrucao, StringComparison.OrdinalIgnoreCase))
                {
                    texto = texto.Substring(instrucao.Length).Trim();
                    break;
                }
            }

            return texto.Trim();
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

            // NOVOS CAMPOS ESPECÍFICOS
            public string? numero_ait { get; set; }
            public string? placa_veiculo { get; set; }
            public string? nome_condutor { get; set; }
            public string? numero_cnh { get; set; }
            public string? texto_defesa { get; set; }
            public string? data_infracao { get; set; }
            public string? local_infracao { get; set; }
            public string? codigo_infracao { get; set; }
            public string? valor_multa { get; set; }
            public string? orgao_autuador { get; set; }

            // CAMPOS PARA INDICAÇÃO DE CONDUTOR
            // Dados do REQUERENTE (proprietário)
            public string? requerente_nome { get; set; }
            public string? requerente_cpf { get; set; }
            public string? requerente_rg { get; set; }
            public string? requerente_endereco { get; set; }

            // Dados da INDICAÇÃO (condutor real)
            public string? indicacao_nome { get; set; }
            public string? indicacao_cpf { get; set; }
            public string? indicacao_rg { get; set; }
            public string? indicacao_cnh { get; set; }

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

        // Métodos helper para conversão de dados
        private DateTime? TentarConverterData(string? dataStr)
        {
            if (string.IsNullOrWhiteSpace(dataStr))
                return null;

            // Tentar converter formato DD/MM/AAAA
            if (DateTime.TryParseExact(dataStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var data))
                return data;

            // Tentar outros formatos comuns
            if (DateTime.TryParse(dataStr, out var dataGenerica))
                return dataGenerica;

            return null;
        }

        private decimal? TentarConverterValor(string? valorStr)
        {
            if (string.IsNullOrWhiteSpace(valorStr))
                return null;

            // Remover caracteres não numéricos exceto vírgula e ponto
            var valorLimpo = System.Text.RegularExpressions.Regex.Replace(valorStr, @"[^\d,.]", "");

            // Substituir vírgula por ponto para conversão
            valorLimpo = valorLimpo.Replace(",", ".");

            if (decimal.TryParse(valorLimpo, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                return valor;

            return null;
        }
    }
}
