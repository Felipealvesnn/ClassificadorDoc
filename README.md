# Classificador de Documentos PDF

API para classificação automática de documentos de trânsito PDF usando IA (Gemini ou OpenAI).

## Configuração

### 1. Escolher o provedor de IA

**ATUALMENTE CONFIGURADO: Google Gemini (gratuito)**

#### Opção A: Usando Google Gemini (ATIVO)

Edite o arquivo `appsettings.json` e adicione sua chave do Gemini:

```json
{
  "Gemini": {
    "ApiKey": "sua-chave-gemini-aqui"
  }
}
```

Obtenha sua chave gratuita em: https://aistudio.google.com/app/apikey

#### Opção B: Alternando para OpenAI

Se quiser usar OpenAI em vez do Gemini, edite o `Program.cs`:

1. Comente a seção "OPÇÃO 1: Usando Gemini"
2. Descomente a seção "OPÇÃO 2: Usando OpenAI"
3. Configure a chave no `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sk-sua-chave-openai-aqui"
  }
}
```

### 2. Instalar dependências

```bash
dotnet restore
```

### 3. Executar a aplicação

```bash
dotnet run
```

A API estará disponível em `https://localhost:5001` ou `http://localhost:5000`.

## Endpoints

### 🔤 POST /api/classificador/classificar-zip (EXTRAÇÃO DE TEXTO)

Classifica documentos PDF extraindo texto primeiro e analisando o conteúdo textual.

**Vantagens:** Mais rápido e barato  
**Limitações:** Não analisa elementos visuais, PDFs escaneados podem não funcionar

### 👁️ POST /api/classificador/classificar-zip-visual (ANÁLISE VISUAL COMPLETA)

Classifica documentos PDF enviando-os diretamente para análise visual da IA.

**Vantagens:** Analisa layout, carimbos, assinaturas, PDFs escaneados  
**Limitações:** Mais lento e pode ser mais caro

### 🔤 POST /api/classificador/classificar-pdf-individual (TEXTO)

Classifica um único PDF extraindo texto.

### 👁️ POST /api/classificador/classificar-pdf-visual (VISUAL)

Classifica um único PDF com análise visual completa.

**Parâmetros (todos os endpoints):**

- `arquivo`: Arquivo ZIP contendo PDFs ou PDF individual (multipart/form-data)

**Resposta (mesma para todos):**

```json
{
  "totalDocumentos": 3,
  "documentosProcessados": 3,
  "documentosComErro": 0,
  "tempoProcessamento": "00:00:45.123",
  "documentos": [
    {
      "nomeArquivo": "auto_infracao_001.pdf",
      "tipoDocumento": "autuacao",
      "confiancaClassificacao": 0.95,
      "resumoConteudo": "Auto de infração por excesso de velocidade...",
      "palavrasChaveEncontradas": "auto de infração, AIT, agente de trânsito",
      "textoExtraido": "AUTO DE INFRAÇÃO DE TRÂNSITO\nO condutor do veículo placa ABC-1234 foi autuado por excesso de velocidade...",
      "processadoComSucesso": true,
      "erroProcessamento": null
    },
    {
      "nomeArquivo": "defesa_recurso_002.pdf",
      "tipoDocumento": "defesa",
      "confiancaClassificacao": 0.92,
      "resumoConteudo": "Defesa prévia contestando autuação por não ser o condutor...",
      "palavrasChaveEncontradas": "defesa prévia, contestação, não era o condutor",
      "textoExtraido": "DEFESA PRÉVIA\nVenho por meio desta apresentar defesa prévia contra a autuação...",
      "processadoComSucesso": true,
      "erroProcessamento": null
    },
    {
      "nomeArquivo": "notificacao_003.pdf",
      "tipoDocumento": "notificacao_penalidade",
      "confiancaClassificacao": 0.98,
      "resumoConteudo": "Notificação da penalidade confirmada, prazo para pagamento...",
      "palavrasChaveEncontradas": "notificação da penalidade, NIP, valor da multa",
      "textoExtraido": "NOTIFICAÇÃO DA PENALIDADE\nFica V.Sa. notificado da aplicação da penalidade...",
      "processadoComSucesso": true,
      "erroProcessamento": null
    }
  ]
}
```

## 🔤 vs 👁️ Qual modo escolher?

### MODO TEXTO (mais rápido)

**Use quando:**

- PDFs com texto bem definido
- Documentos digitais (não escaneados)
- Processamento em lote rápido
- Economia de custos de API

### MODO VISUAL (mais preciso)

**Use quando:**

- PDFs escaneados ou de baixa qualidade
- Documentos com elementos visuais importantes (carimbos, assinaturas)
- Necessita analisar layout e formatação
- Máxima precisão na classificação

**Recomendação:** Comece com o modo texto. Se a precisão não for satisfatória, teste o modo visual.

### GET /api/classificador/tipos-documento

Retorna os tipos de documento disponíveis para classificação.

### GET /api/classificador/status

Verifica o status da API.

## Tipos de Documento de Trânsito

- **autuacao**: Auto de Infração de Trânsito (AIT), Notificação de Autuação - documentos onde se constata e registra uma infração
- **defesa**: Defesa de Autuação, Recurso JARI (1ª instância), Recurso CETRAN (2ª instância), Defesa Prévia, Indicação de Condutor
- **notificacao_penalidade**: Notificação da Penalidade (NIP), Intimação para pagamento de multa - comunicação oficial da penalidade confirmada
- **outros**: Outros documentos de trânsito que não se encaixam nas categorias acima

## Campos Retornados

- **nomeArquivo**: Nome do arquivo PDF original
- **tipoDocumento**: Classificação do documento (autuacao, defesa, notificacao_penalidade, outros)
- **confiancaClassificacao**: Nível de confiança da IA na classificação (0.0 a 1.0)
- **resumoConteudo**: Resumo do que foi identificado no documento
- **palavrasChaveEncontradas**: Termos específicos que justificaram a classificação
- **textoExtraido**: Texto completo extraído do PDF
- **processadoComSucesso**: Se o documento foi processado sem erros
- **erroProcessamento**: Mensagem de erro, caso tenha ocorrido

## Limitações

- Tamanho máximo do arquivo ZIP: 100MB
- Processamento em batches de 5 PDFs por vez
- Texto limitado a 4000 caracteres por documento para análise da IA

## Tecnologias Utilizadas

- .NET 8
- OpenAI API (GPT-4o-mini)
- iText7 (extração de texto de PDF)
- System.IO.Compression (manipulação de arquivos ZIP)

## Como Testar

1. Prepare um arquivo ZIP com alguns PDFs
2. Use o Swagger UI em `/swagger` para testar o endpoint
3. Ou use curl:

```bash
curl -X POST "https://localhost:5001/api/classificador/classificar-zip" \
  -H "Content-Type: multipart/form-data" \
  -F "arquivo=@caminho/para/seus/documentos.zip"
```
