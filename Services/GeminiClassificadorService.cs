using ClassificadorDoc.Models;
using System.Text.Json;
using System.Text;
using Mscc.GenerativeAI;

namespace ClassificadorDoc.Services
{
    public class GeminiClassificadorService : IClassificadorService
    {
        private readonly string _apiKey;
        private readonly ILogger<GeminiClassificadorService> _logger;

        public GeminiClassificadorService(IConfiguration configuration, ILogger<GeminiClassificadorService> logger)
        {
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API Key não configurada");
            _logger = logger;
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoAsync(string nomeArquivo, string textoDocumento)
        {
            const int maxTentativas = 3;
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                try
                {
                    var prompt = CriarPromptClassificacao(textoDocumento);

                    var googleAI = new GoogleAI(_apiKey);
                    var model = googleAI.GenerativeModel("gemini-1.5-flash");

                    var response = await model.GenerateContent(prompt);
                    var textoResposta = response.Text;

                    if (string.IsNullOrEmpty(textoResposta))
                    {
                        throw new InvalidOperationException("Resposta vazia do Gemini");
                    }

                    // Extrair JSON da resposta que pode vir em bloco markdown
                    var jsonLimpo = ExtrairJsonDaResposta(textoResposta);
                    var classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(jsonLimpo);

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
                        PalavrasChaveEncontradas = classificacao.GetPalavrasChaveComoString(),
                        TextoExtraido = textoDocumento,
                        ProcessadoComSucesso = true
                    };
                }
                catch (Exception ex) when (tentativa < maxTentativas &&
                    (ex.Message.Contains("inner stream position") ||
                     ex.Message.Contains("stream") ||
                     ex.Message.Contains("position") ||
                     ex.Message.Contains("timeout") ||
                     ex.Message.Contains("network") ||
                     ex.Message.Contains("concurrency") ||
                     ex.Message.Contains("thread")))
                {
                    ultimaExcecao = ex;
                    _logger.LogWarning("Tentativa {Tentativa} falhou para {NomeArquivo}: {Erro}. Tentando novamente...",
                        tentativa, nomeArquivo, ex.Message);

                    // Aguarda antes de tentar novamente
                    await Task.Delay(TimeSpan.FromSeconds(tentativa * 2));
                    continue;
                }
                catch (Exception ex)
                {
                    ultimaExcecao = ex;
                    break;
                }
            }

            _logger.LogError(ultimaExcecao, "Erro ao classificar documento {NomeArquivo} com Gemini após {MaxTentativas} tentativas",
                nomeArquivo, maxTentativas);

            return new DocumentoClassificacao
            {
                NomeArquivo = nomeArquivo,
                TipoDocumento = "Erro",
                ConfiancaClassificacao = 0,
                ResumoConteudo = "Erro no processamento",
                TextoExtraido = textoDocumento ?? string.Empty,
                ProcessadoComSucesso = false,
                ErroProcessamento = ultimaExcecao?.Message ?? "Erro desconhecido após múltiplas tentativas"
            };
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoPdfAsync(string nomeArquivo, byte[] pdfBytes)
        {
            return await ClassificarDocumentoVisualAsync(nomeArquivo, pdfBytes, "application/pdf");
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoImagemAsync(string nomeArquivo, byte[] imagemBytes, string mimeType = "image/jpeg")
        {
            return await ClassificarDocumentoVisualAsync(nomeArquivo, imagemBytes, mimeType);
        }

        private async Task<DocumentoClassificacao> ClassificarDocumentoVisualAsync(string nomeArquivo, byte[] arquivoBytes, string mimeType)
        {
            const int maxTentativas = 3;
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                GoogleAI? googleAI = null;
                GenerativeModel? model = null;

                try
                {
                    // Cria instâncias completamente novas para cada tentativa
                    googleAI = new GoogleAI(_apiKey);
                    model = googleAI.GenerativeModel("gemini-1.5-flash");

                    // Validação básica do arquivo
                    if (arquivoBytes == null || arquivoBytes.Length == 0)
                    {
                        throw new ArgumentException("Arquivo vazio ou inválido");
                    }

                    var base64Arquivo = Convert.ToBase64String(arquivoBytes);

                    // Criando o prompt adaptado para o tipo de arquivo
                    var prompt = CriarPromptClassificacaoVisual(mimeType);

                    // Log adicional para PDFs que podem ser escaneados
                    if (mimeType.Contains("pdf"))
                    {
                        _logger.LogInformation("Processando PDF para {NomeArquivo} - pode ser texto nativo ou escaneado", nomeArquivo);
                    }

                    // Usando o método simples apenas com texto por enquanto
                    // TODO: Implementar envio de arquivo visual quando encontrarmos a API correta
                    var response = await model.GenerateContent(prompt);
                    var textoResposta = response?.Text;

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
                        TextoExtraido = $"[{tipoAnalise} analisado diretamente pelo Gemini - análise visual completa com OCR se necessário]",
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
                     ex.Message.Contains("thread")))
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
                finally
                {
                    // Limpa referências para ajudar no GC
                    model = null;
                    googleAI = null;
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

Analise cuidadosamente o conteúdo preenchido e retorne APENAS um JSON válido no seguinte formato exato (sem blocos de código markdown):
{{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
    ""confianca"": [0.0-1.0],
    ""resumo"": ""Descreva o que foi identificado no documento de trânsito em até 200 caracteres"",
    ""palavras_chave_encontradas"": ""Lista as principais palavras como STRING separadas por vírgula""
}}
";
        }

        private string CriarPromptClassificacaoVisual(string mimeType)
        {
            var tipoArquivo = mimeType.Contains("pdf") ? "PDF" : "imagem";
            var instrucaoEspecifica = mimeType.Contains("pdf")
                ? "Use a capacidade visual do Gemini para examinar este documento PDF (que pode ser texto nativo ou escaneado):"
                : "Use a capacidade visual do Gemini para examinar esta imagem escaneada de documento:";

            return $@"
Analise este documento de trânsito brasileiro completamente. {instrucaoEspecifica}

IMPORTANTE: Este {tipoArquivo} pode conter:
- Texto nativo (PDF digital com texto selecionável)
- Imagens escaneadas (PDF de digitalização com OCR necessário)
- Fotos de documentos com qualidade variável
- Documentos inclinados, com sombras ou reflexos

ANÁLISE VISUAL COMPLETA:
- Layout e formatação do documento
- Carimbos, assinaturas, logos oficiais
- Campos preenchidos vs vazios
- Elementos gráficos que indiquem o tipo
- Qualidade (original, cópia, digitalização, escaneamento)
- Texto legível mesmo que esteja em ângulo, com qualidade reduzida, brilho/contraste inadequado
- Faça OCR (reconhecimento de texto) se necessário para PDFs escaneados

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
- Texto mesmo que esteja em qualidade de escaneamento, foto ou digitalização
- Números de códigos, placas, valores, mesmo que parcialmente legíveis

ESTRATÉGIA PARA PDFs ESCANEADOS/IMAGENS:
1. Primeiro, tente identificar o tipo pelo layout visual e elementos gráficos
2. Depois, faça o melhor esforço para ler textos usando OCR
3. Se o texto estiver ilegível, baseie-se nos elementos visuais (formulários, campos, logos)
4. Indique na confiança se houve dificuldades de leitura

INSTRUÇÃO ESPECIAL: 
- Para PDFs escaneados: Faça OCR do texto visível mesmo que não esteja perfeito
- Para imagens: Leia o texto mesmo que esteja em ângulo, com sombras ou reflexos
- Se houver dificuldades de leitura, reduza a confiança mas ainda tente classificar
- Priorize elementos visuais consistentes (logos, layout) quando texto for ilegível

Retorne APENAS este JSON (sem blocos de código markdown):
{{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
    ""confianca"": [0.0-1.0],
    ""resumo"": ""Descrição baseada na análise visual e textual completa do {tipoArquivo}"",
    ""palavras_chave_encontradas"": ""Elementos encontrados como STRING separados por vírgula, não como array""
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

Retorne APENAS este JSON (sem blocos de código markdown):
{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
     ""confianca"": [0.0-1.0],
    ""resumo"": ""Descrição baseada na análise visual e textual completa do PDF"",
    ""palavras_chave_encontradas"": ""Elementos encontrados como STRING separados por vírgula, não como array""
}
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
