using OpenAI;
using OpenAI.Chat;
using ClassificadorDoc.Models;
using System.Text.Json;

namespace ClassificadorDoc.Services
{
    public interface IClassificadorService
    {
        Task<DocumentoClassificacao> ClassificarDocumentoAsync(string nomeArquivo, string textoDocumento);
        Task<DocumentoClassificacao> ClassificarDocumentoPdfAsync(string nomeArquivo, byte[] pdfBytes);
    }

    public class ClassificadorService : IClassificadorService
    {
        private readonly OpenAIClient _openAiClient;
        private readonly ILogger<ClassificadorService> _logger;

        public ClassificadorService(OpenAIClient openAiClient, ILogger<ClassificadorService> logger)
        {
            _openAiClient = openAiClient;
            _logger = logger;
        }

        public async Task<DocumentoClassificacao> ClassificarDocumentoAsync(string nomeArquivo, string textoDocumento)
        {
            try
            {
                var prompt = CriarPromptClassificacao(textoDocumento);

                var chatClient = _openAiClient.GetChatClient("gpt-4o-mini");

                var completion = await chatClient.CompleteChatAsync(
                    new ChatMessage[]
                    {
                        new SystemChatMessage("Você é um especialista em classificação de documentos de trânsito brasileiros. Analise o conteúdo preenchido no documento e classifique-o como autuação, defesa ou notificação de penalidade. Retorne APENAS um JSON válido com a classificação."),
                        new UserChatMessage(prompt)
                    },
                    new ChatCompletionOptions
                    {
                        Temperature = 0.1f
                    });

                var resposta = completion.Value.Content[0].Text;
                var classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(resposta);

                if (classificacao == null)
                {
                    throw new InvalidOperationException("Falha ao deserializar resposta da OpenAI");
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
                _logger.LogError(ex, "Erro ao classificar documento {NomeArquivo}", nomeArquivo);
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
                var chatClient = _openAiClient.GetChatClient("gpt-4o");

                var completion = await chatClient.CompleteChatAsync(
                    new ChatMessage[]
                    {
                        new SystemChatMessage("Você é um especialista em classificação de documentos de trânsito brasileiros. Analise o PDF completo (texto, layout, formatação, carimbos) e classifique-o como autuação, defesa ou notificação de penalidade. Retorne APENAS um JSON válido."),
                        new UserChatMessage(
                            ChatMessageContentPart.CreateTextPart(CriarPromptClassificacaoPdf()),
                            ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(pdfBytes), "application/pdf")
                        )
                    },
                    new ChatCompletionOptions
                    {
                        Temperature = 0.1f
                    });

                var resposta = completion.Value.Content[0].Text;
                var classificacao = JsonSerializer.Deserialize<ClassificacaoResposta>(resposta);

                if (classificacao == null)
                {
                    throw new InvalidOperationException("Falha ao deserializar resposta da OpenAI");
                }

                return new DocumentoClassificacao
                {
                    NomeArquivo = nomeArquivo,
                    TipoDocumento = classificacao.tipo_documento,
                    ConfiancaClassificacao = classificacao.confianca,
                    ResumoConteudo = classificacao.resumo,
                    PalavrasChaveEncontradas = classificacao.palavras_chave_encontradas,
                    TextoExtraido = "[PDF analisado diretamente - texto não extraído separadamente]",
                    ProcessadoComSucesso = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao classificar PDF {NomeArquivo} diretamente", nomeArquivo);
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
Você é um especialista em documentos de trânsito brasileiros. Analise o CONTEÚDO ESPECÍFICO preenchido no documento e classifique-o baseado no que está escrito, considerando que é um documento do sistema de trânsito.

CONTEXTO: Documentos de trânsito podem usar o mesmo formulário base, mas o conteúdo preenchido determina a finalidade.

TIPOS DE DOCUMENTO DE TRÂNSITO:
- autuacao: Auto de Infração de Trânsito (AIT), Notificação de Autuação, lavrado por agente, constatação de infração
- defesa: Defesa de Autuação, Recurso 1ª instância (JARI), Recurso 2ª instância (CETRAN), Defesa Prévia, Indicação de Condutor
- notificacao_penalidade: Notificação da Penalidade (NIP), Intimação para pagamento de multa, comunicação de penalidade aplicada
- outros: Documentos de trânsito que não se encaixam nas categorias acima

PALAVRAS-CHAVE ESPECÍFICAS DE TRÂNSITO:
AUTUAÇÃO: ""auto de infração"", ""AIT"", ""notificação de autuação"", ""infração de trânsito"", ""foi autuado"", ""lavrado"", ""agente de trânsito"", ""constatou-se"", ""irregularidade"", ""código de trânsito""

DEFESA: ""defesa de autuação"", ""recurso"", ""impugnação"", ""JARI"", ""CETRAN"", ""defesa prévia"", ""contestação"", ""indicação de condutor"", ""não era o condutor"", ""alegações"", ""discordância"", ""fundamentação""

NOTIFICAÇÃO PENALIDADE: ""notificação da penalidade"", ""NIP"", ""fica notificado"", ""multa aplicada"", ""valor da multa"", ""prazo para pagamento"", ""penalidade de trânsito"", ""infração confirmada"", ""débito""

DOCUMENTO PARA ANÁLISE:
{textoLimitado}

Analise cuidadosamente o conteúdo e identifique:
1. Se é uma autuação sendo lavrada
2. Se é uma defesa/recurso sendo apresentado  
3. Se é uma notificação de penalidade confirmada
4. As palavras-chave que justificam a classificação

Retorne APENAS um JSON válido:
{{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
    ""confianca"": [0.0-1.0],
    ""resumo"": ""Descreva o que foi identificado no documento de trânsito em até 200 caracteres"",
    ""palavras_chave_encontradas"": ""Principais termos de trânsito que justificaram a classificação""
}}
";
        }

        private string CriarPromptClassificacaoPdf()
        {
            return @"
Analise este documento PDF de trânsito brasileiro. Considere TODOS os elementos visuais:

INSTRUÇÕES ESPECÍFICAS PARA PDF:
- Observe layout, formatação, carimbos, assinaturas
- Analise campos preenchidos vs em branco
- Considere logos de órgãos (DETRAN, PRF, etc.)
- Veja elementos visuais que indiquem o tipo de documento

TIPOS DE DOCUMENTO DE TRÂNSITO:
- autuacao: Auto de Infração de Trânsito (AIT), Notificação de Autuação - documento inicial da infração
- defesa: Defesa de Autuação, Recurso JARI/CETRAN, Defesa Prévia, Indicação de Condutor - contestação
- notificacao_penalidade: Notificação da Penalidade (NIP), Intimação para pagamento - confirmação da multa
- outros: Outros documentos de trânsito

ELEMENTOS VISUAIS IMPORTANTES:
AUTUAÇÃO: Campos do agente, local/data da infração, descrição da irregularidade, código CTB
DEFESA: Texto argumentativo, alegações, pedidos, assinatura do requerente  
NOTIFICAÇÃO: Valores de multa, prazos de pagamento, dados bancários, confirmação da penalidade

ANÁLISE SOLICITADA:
1. Identifique o tipo baseado no conteúdo E layout
2. Observe se é documento original ou cópia
3. Verifique preenchimento de campos específicos
4. Analise elementos que confirmem a classificação

Retorne APENAS este JSON:
{
    ""tipo_documento"": ""[autuacao|defesa|notificacao_penalidade|outros]"",
    ""confianca"": 0.95,
    ""resumo"": ""Descrição baseada na análise visual e textual do PDF"",
    ""palavras_chave_encontradas"": ""Elementos visuais e textuais que justificaram a classificação""
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
    }
}
