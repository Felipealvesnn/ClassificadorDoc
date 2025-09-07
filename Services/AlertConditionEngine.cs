using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ClassificadorDoc.Services
{
    /// <summary>
    /// Engine para avaliação de condições de alertas
    /// Suporta condições simples e complexas usando JSON
    /// Requisito 4.2.6 - Modelagem sem programação
    /// </summary>
    public interface IAlertConditionEngine
    {
        Task<bool> EvaluateConditionAsync(string condition, Dictionary<string, object> context);
        string ValidateCondition(string condition);
        List<dynamic> GetAvailableVariables();
        List<AlertTemplate> GetPredefinedTemplates();
    }

    public class AlertConditionEngine : IAlertConditionEngine
    {
        private readonly ILogger<AlertConditionEngine> _logger;

        public AlertConditionEngine(ILogger<AlertConditionEngine> logger)
        {
            _logger = logger;
        }

        public async Task<bool> EvaluateConditionAsync(string condition, Dictionary<string, object> context)
        {
            try
            {
                // Verificar se é JSON (condição complexa) ou string simples
                if (IsJsonCondition(condition))
                {
                    return await EvaluateJsonConditionAsync(condition, context);
                }
                else
                {
                    return await EvaluateSimpleConditionAsync(condition, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao avaliar condição: {Condition}", condition);
                return false;
            }
        }

        public string ValidateCondition(string condition)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(condition))
                {
                    return "Condição não pode estar vazia";
                }

                if (IsJsonCondition(condition))
                {
                    return ValidateJsonCondition(condition);
                }
                else
                {
                    return ValidateSimpleCondition(condition);
                }
            }
            catch (Exception ex)
            {
                return $"Erro na validação: {ex.Message}";
            }
        }

        public List<dynamic> GetAvailableVariables()
        {
            return new List<dynamic>
            {
                // === MÉTRICAS DE USUÁRIOS ===
                new { Name = "active_users", Description = "Usuários ativos no sistema" },
                new { Name = "users_logged_today", Description = "Usuários que fizeram login hoje" },
                new { Name = "DaysSinceLastActivity", Description = "Dias desde a última atividade do usuário" },
                new { Name = "new_users_today", Description = "Novos usuários cadastrados hoje" },
                new { Name = "users_inactive_7days", Description = "Usuários inativos há mais de 7 dias" },
                
                // === MÉTRICAS DE DOCUMENTOS ===
                new { Name = "documents_today", Description = "Documentos processados hoje" },
                new { Name = "documents_last_hour", Description = "Documentos processados na última hora" },
                new { Name = "documents_this_week", Description = "Documentos processados esta semana" },
                new { Name = "documents_pending", Description = "Documentos aguardando processamento" },
                new { Name = "documents_failed_today", Description = "Documentos que falharam hoje" },
                
                // === MÉTRICAS DE QUALIDADE ===
                new { Name = "error_rate_today", Description = "Taxa de erro hoje (percentual)" },
                new { Name = "error_rate_week", Description = "Taxa de erro desta semana (percentual)" },
                new { Name = "avg_confidence_today", Description = "Confiança média das classificações hoje (percentual)" },
                new { Name = "low_confidence_count", Description = "Documentos com baixa confiança hoje" },
                new { Name = "success_rate", Description = "Taxa de sucesso geral (percentual)" },
                
                // === MÉTRICAS DE PERFORMANCE ===
                new { Name = "processing_time_avg", Description = "Tempo médio de processamento (segundos)" },
                new { Name = "processing_time_max", Description = "Maior tempo de processamento hoje (segundos)" },
                new { Name = "api_response_time", Description = "Tempo de resposta da API (ms)" },
                new { Name = "queue_size", Description = "Tamanho da fila de processamento" },
                new { Name = "queue_wait_time", Description = "Tempo médio de espera na fila (minutos)" },
                
                // === MÉTRICAS DE LOTES ===
                new { Name = "batches_today", Description = "Lotes processados hoje" },
                new { Name = "failed_batches_today", Description = "Lotes que falharam hoje" },
                new { Name = "batches_in_progress", Description = "Lotes em processamento no momento" },
                new { Name = "avg_batch_size", Description = "Tamanho médio dos lotes hoje" },
                new { Name = "largest_batch_today", Description = "Maior lote processado hoje" },
                
                // === MÉTRICAS DE SISTEMA ===
                new { Name = "system_cpu_usage", Description = "Uso de CPU do sistema (percentual)" },
                new { Name = "system_memory_usage", Description = "Uso de memória do sistema (percentual)" },
                new { Name = "disk_space_available", Description = "Espaço em disco disponível (GB)" },
                new { Name = "database_connections", Description = "Conexões ativas no banco de dados" },
                new { Name = "database_response_time", Description = "Tempo de resposta do banco (ms)" },
                
                // === MÉTRICAS TEMPORAIS ===
                new { Name = "current_hour", Description = "Hora atual (0-23)" },
                new { Name = "current_date", Description = "Data atual" },
                new { Name = "current_day_of_week", Description = "Dia da semana (1=Domingo, 7=Sábado)" },
                new { Name = "is_weekend", Description = "Se é final de semana (true/false)" },
                new { Name = "is_business_hours", Description = "Se está em horário comercial (true/false)" },
                
                // === MÉTRICAS DE CLASSIFICAÇÃO ===
                new { Name = "edital_docs_today", Description = "Documentos de editais processados hoje" },
                new { Name = "contract_docs_today", Description = "Documentos de contratos processados hoje" },
                new { Name = "invoice_docs_today", Description = "Documentos de faturas processados hoje" },
                new { Name = "unclassified_docs_today", Description = "Documentos não classificados hoje" },
                new { Name = "reclassified_docs_today", Description = "Documentos reclassificados hoje" },
                
                // === MÉTRICAS DE ALERTAS ===
                new { Name = "alerts_triggered_today", Description = "Alertas disparados hoje" },
                new { Name = "critical_alerts_active", Description = "Alertas críticos ativos" },
                new { Name = "alerts_resolved_today", Description = "Alertas resolvidos hoje" },
                
                // === MÉTRICAS DE STORAGE ===
                new { Name = "total_files_size_gb", Description = "Tamanho total dos arquivos (GB)" },
                new { Name = "files_uploaded_today", Description = "Arquivos enviados hoje" },
                new { Name = "avg_file_size_mb", Description = "Tamanho médio dos arquivos (MB)" },
                new { Name = "duplicate_files_found", Description = "Arquivos duplicados encontrados" }
            };
        }

        public List<AlertTemplate> GetPredefinedTemplates()
        {
            return new List<AlertTemplate>
            {
                // === TEMPLATES DE QUALIDADE ===
                new AlertTemplate
                {
                    Name = "Alta Taxa de Erro",
                    Description = "Dispara quando a taxa de erro excede um limite",
                    Condition = "error_rate_today > 10",
                    Category = "QUALIDADE"
                },
                new AlertTemplate
                {
                    Name = "Baixa Confiança nas Classificações",
                    Description = "Dispara quando muitos documentos têm baixa confiança",
                    Condition = "low_confidence_count > 20",
                    Category = "QUALIDADE"
                },
                new AlertTemplate
                {
                    Name = "Muitos Documentos Não Classificados",
                    Description = "Dispara quando há muitos documentos sem classificação",
                    Condition = "unclassified_docs_today > 50",
                    Category = "QUALIDADE"
                },
                
                // === TEMPLATES DE PRODUTIVIDADE ===
                new AlertTemplate
                {
                    Name = "Baixa Produtividade",
                    Description = "Dispara quando poucos documentos são processados",
                    Condition = "documents_today < 50",
                    Category = "PRODUTIVIDADE"
                },
                new AlertTemplate
                {
                    Name = "Fila de Processamento Muito Grande",
                    Description = "Dispara quando há muitos documentos aguardando",
                    Condition = "queue_size > 1000",
                    Category = "PRODUTIVIDADE"
                },
                new AlertTemplate
                {
                    Name = "Processamento Muito Lento",
                    Description = "Dispara quando o tempo de processamento está alto",
                    Condition = "processing_time_avg > 30",
                    Category = "PRODUTIVIDADE"
                },
                
                // === TEMPLATES DE SISTEMA ===
                new AlertTemplate
                {
                    Name = "Muitos Usuários Ativos",
                    Description = "Dispara quando há muitos usuários simultâneos",
                    Condition = "active_users > 100",
                    Category = "SISTEMA"
                },
                new AlertTemplate
                {
                    Name = "Alto Uso de CPU",
                    Description = "Dispara quando o uso de CPU está muito alto",
                    Condition = "system_cpu_usage > 85",
                    Category = "SISTEMA"
                },
                new AlertTemplate
                {
                    Name = "Pouco Espaço em Disco",
                    Description = "Dispara quando o espaço em disco está baixo",
                    Condition = "disk_space_available < 10",
                    Category = "SISTEMA"
                },
                new AlertTemplate
                {
                    Name = "Banco de Dados Lento",
                    Description = "Dispara quando o banco está respondendo devagar",
                    Condition = "database_response_time > 2000",
                    Category = "SISTEMA"
                },
                
                // === TEMPLATES TEMPORAIS ===
                new AlertTemplate
                {
                    Name = "Horário de Expediente",
                    Description = "Dispara apenas em horário comercial",
                    Condition = "current_hour >= 8 AND current_hour <= 18",
                    Category = "TEMPORAL"
                },
                new AlertTemplate
                {
                    Name = "Final de Semana",
                    Description = "Dispara apenas nos finais de semana",
                    Condition = "is_weekend = true",
                    Category = "TEMPORAL"
                },
                new AlertTemplate
                {
                    Name = "Fora do Horário Comercial",
                    Description = "Dispara fora do horário de trabalho",
                    Condition = "is_business_hours = false",
                    Category = "TEMPORAL"
                },
                
                // === TEMPLATES DE LOTES ===
                new AlertTemplate
                {
                    Name = "Lote Falhado",
                    Description = "Dispara quando há lotes com falha",
                    Condition = "failed_batches_today > 0",
                    Category = "ERRO"
                },
                new AlertTemplate
                {
                    Name = "Muitos Lotes em Processamento",
                    Description = "Dispara quando há muitos lotes sendo processados",
                    Condition = "batches_in_progress > 5",
                    Category = "LOTE"
                },
                new AlertTemplate
                {
                    Name = "Lote Muito Grande",
                    Description = "Dispara quando um lote muito grande é processado",
                    Condition = "largest_batch_today > 5000",
                    Category = "LOTE"
                },
                
                // === TEMPLATES DE USUÁRIO ===
                new AlertTemplate
                {
                    Name = "Usuário Inativo",
                    Description = "Dispara quando um usuário fica inativo por muitos dias",
                    Condition = "DaysSinceLastActivity > 7",
                    Category = "USUARIO"
                },
                new AlertTemplate
                {
                    Name = "Muitos Usuários Inativos",
                    Description = "Dispara quando há muitos usuários inativos",
                    Condition = "users_inactive_7days > 10",
                    Category = "USUARIO"
                },
                new AlertTemplate
                {
                    Name = "Picos de Novos Usuários",
                    Description = "Dispara quando há muitos novos usuários em um dia",
                    Condition = "new_users_today > 20",
                    Category = "USUARIO"
                },
                
                // === TEMPLATES DE ALERTAS ===
                new AlertTemplate
                {
                    Name = "Muitos Alertas Críticos",
                    Description = "Dispara quando há muitos alertas críticos ativos",
                    Condition = "critical_alerts_active > 5",
                    Category = "ALERTA"
                },
                new AlertTemplate
                {
                    Name = "Sobrecarga de Alertas",
                    Description = "Dispara quando muitos alertas são disparados",
                    Condition = "alerts_triggered_today > 100",
                    Category = "ALERTA"
                },
                
                // === TEMPLATES COMBINADOS ===
                new AlertTemplate
                {
                    Name = "Sistema Sobrecarregado",
                    Description = "Múltiplas métricas indicando sobrecarga",
                    Condition = "system_cpu_usage > 80 AND queue_size > 500 AND processing_time_avg > 20",
                    Category = "SISTEMA"
                },
                new AlertTemplate
                {
                    Name = "Problemas de Qualidade",
                    Description = "Múltiplos indicadores de problemas na classificação",
                    Condition = "error_rate_today > 5 AND low_confidence_count > 10 AND unclassified_docs_today > 30",
                    Category = "QUALIDADE"
                }
            };
        }

        private bool IsJsonCondition(string condition)
        {
            condition = condition.Trim();
            return condition.StartsWith("{") && condition.EndsWith("}");
        }

        private async Task<bool> EvaluateJsonConditionAsync(string condition, Dictionary<string, object> context)
        {
            var conditionObj = JsonConvert.DeserializeObject<JObject>(condition);

            if (conditionObj == null)
                return false;

            return await EvaluateConditionObject(conditionObj, context);
        }

        private async Task<bool> EvaluateConditionObject(JObject conditionObj, Dictionary<string, object> context)
        {
            // Suporte para operadores lógicos
            if (conditionObj.ContainsKey("AND"))
            {
                var conditions = conditionObj["AND"] as JArray;
                if (conditions != null)
                {
                    foreach (var cond in conditions)
                    {
                        if (cond is JObject subCondition)
                        {
                            if (!await EvaluateConditionObject(subCondition, context))
                                return false;
                        }
                    }
                    return true;
                }
            }

            if (conditionObj.ContainsKey("OR"))
            {
                var conditions = conditionObj["OR"] as JArray;
                if (conditions != null)
                {
                    foreach (var cond in conditions)
                    {
                        if (cond is JObject subCondition)
                        {
                            if (await EvaluateConditionObject(subCondition, context))
                                return true;
                        }
                    }
                    return false;
                }
            }

            // Condições simples
            if (conditionObj.ContainsKey("variable") && conditionObj.ContainsKey("operator") && conditionObj.ContainsKey("value"))
            {
                var variable = conditionObj["variable"]?.ToString();
                var operatorStr = conditionObj["operator"]?.ToString();
                var value = conditionObj["value"];

                return EvaluateSimpleComparison(variable, operatorStr, value, context);
            }

            return false;
        }

        private async Task<bool> EvaluateSimpleConditionAsync(string condition, Dictionary<string, object> context)
        {
            // Suporte para condições simples como "error_rate_today > 10"
            condition = condition.Trim();

            // Operadores AND/OR
            if (condition.Contains(" AND "))
            {
                var parts = condition.Split(new[] { " AND " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (!await EvaluateSimpleConditionAsync(part.Trim(), context))
                        return false;
                }
                return true;
            }

            if (condition.Contains(" OR "))
            {
                var parts = condition.Split(new[] { " OR " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (await EvaluateSimpleConditionAsync(part.Trim(), context))
                        return true;
                }
                return false;
            }

            // Condições de comparação simples
            var operators = new[] { ">=", "<=", "!=", ">", "<", "=" };

            foreach (var op in operators)
            {
                if (condition.Contains($" {op} "))
                {
                    var parts = condition.Split(new[] { $" {op} " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var variable = parts[0].Trim();
                        var valueStr = parts[1].Trim();

                        // Tentar converter o valor
                        object value = valueStr;
                        if (double.TryParse(valueStr, out double numValue))
                        {
                            value = numValue;
                        }
                        else if (bool.TryParse(valueStr, out bool boolValue))
                        {
                            value = boolValue;
                        }
                        else if (DateTime.TryParse(valueStr, out DateTime dateValue))
                        {
                            value = dateValue;
                        }

                        return EvaluateSimpleComparison(variable, op, value, context);
                    }
                }
            }

            return false;
        }

        private bool EvaluateSimpleComparison(string? variable, string? operatorStr, object? value, Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(variable) || string.IsNullOrEmpty(operatorStr) || value == null)
                return false;

            if (!context.ContainsKey(variable))
            {
                _logger.LogWarning("Variável não encontrada no contexto: {Variable}", variable);
                return false;
            }

            var contextValue = context[variable];

            // Comparações numéricas
            if (contextValue is IComparable contextComparable && value is IComparable valueComparable)
            {
                var comparison = contextComparable.CompareTo(Convert.ChangeType(value, contextValue.GetType()));

                return operatorStr switch
                {
                    ">" => comparison > 0,
                    ">=" => comparison >= 0,
                    "<" => comparison < 0,
                    "<=" => comparison <= 0,
                    "=" => comparison == 0,
                    "!=" => comparison != 0,
                    _ => false
                };
            }

            return false;
        }

        private string ValidateJsonCondition(string condition)
        {
            try
            {
                var conditionObj = JsonConvert.DeserializeObject<JObject>(condition);
                if (conditionObj == null)
                {
                    return "JSON inválido";
                }

                // Validar estrutura básica
                return ValidateConditionStructure(conditionObj);
            }
            catch (JsonException ex)
            {
                return $"JSON inválido: {ex.Message}";
            }
        }

        private string ValidateConditionStructure(JObject conditionObj)
        {
            var supportedKeys = new[] { "AND", "OR", "variable", "operator", "value" };
            var supportedOperators = new[] { ">", ">=", "<", "<=", "=", "!=" };
            var availableVars = GetAvailableVariables().Select(v => ((dynamic)v).Name).ToList();

            foreach (var prop in conditionObj.Properties())
            {
                if (!supportedKeys.Contains(prop.Name))
                {
                    return $"Chave não suportada: {prop.Name}";
                }

                if (prop.Name == "variable" && !availableVars.Contains(prop.Value?.ToString() ?? ""))
                {
                    return $"Variável não disponível: {prop.Value}";
                }

                if (prop.Name == "operator" && !supportedOperators.Contains(prop.Value?.ToString() ?? ""))
                {
                    return $"Operador não suportado: {prop.Value}";
                }
            }

            return "OK";
        }

        private string ValidateSimpleCondition(string condition)
        {
            var availableVars = GetAvailableVariables().Select(v => ((dynamic)v).Name).ToList();
            var operators = new[] { ">=", "<=", "!=", ">", "<", "=" };

            // Verificar se contém operadores válidos
            bool hasOperator = operators.Any(op => condition.Contains($" {op} "));
            if (!hasOperator)
            {
                return "Condição deve conter um operador válido (>, >=, <, <=, =, !=)";
            }

            // Verificar variáveis
            foreach (var variable in availableVars)
            {
                if (condition.Contains(variable))
                {
                    return "OK";
                }
            }

            return "Nenhuma variável válida encontrada na condição";
        }
    }

    /// <summary>
    /// Template de alerta pré-definido
    /// </summary>
    public class AlertTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
