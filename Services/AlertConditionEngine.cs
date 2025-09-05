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
                new { Name = "active_users", Description = "Usuários ativos no sistema" },
                new { Name = "documents_today", Description = "Documentos processados hoje" },
                new { Name = "error_rate_today", Description = "Taxa de erro hoje (percentual)" },
                new { Name = "batches_today", Description = "Lotes processados hoje" },
                new { Name = "current_hour", Description = "Hora atual (0-23)" },
                new { Name = "current_date", Description = "Data atual" },
                new { Name = "documents_last_hour", Description = "Documentos processados na última hora" },
                new { Name = "avg_confidence_today", Description = "Confiança média das classificações hoje (percentual)" },
                new { Name = "failed_batches_today", Description = "Lotes que falharam hoje" },
                new { Name = "users_logged_today", Description = "Usuários que fizeram login hoje" },
                new { Name = "processing_time_avg", Description = "Tempo médio de processamento (segundos)" },
                new { Name = "success_rate", Description = "Taxa de sucesso geral (percentual)" },
                new { Name = "system_cpu_usage", Description = "Uso de CPU do sistema (percentual)" },
                new { Name = "system_memory_usage", Description = "Uso de memória do sistema (percentual)" },
                new { Name = "queue_size", Description = "Tamanho da fila de processamento" },
                new { Name = "api_response_time", Description = "Tempo de resposta da API (ms)" },
                new { Name = "disk_space_available", Description = "Espaço em disco disponível (GB)" },
                new { Name = "database_connections", Description = "Conexões ativas no banco de dados" }
            };
        }

        public List<AlertTemplate> GetPredefinedTemplates()
        {
            return new List<AlertTemplate>
            {
                new AlertTemplate
                {
                    Name = "Alta Taxa de Erro",
                    Description = "Dispara quando a taxa de erro excede um limite",
                    Condition = "error_rate_today > 10",
                    Category = "QUALIDADE"
                },
                new AlertTemplate
                {
                    Name = "Baixa Produtividade",
                    Description = "Dispara quando poucos documentos são processados",
                    Condition = "documents_today < 50",
                    Category = "PRODUTIVIDADE"
                },
                new AlertTemplate
                {
                    Name = "Muitos Usuários Ativos",
                    Description = "Dispara quando há muitos usuários simultâneos",
                    Condition = "active_users > 100",
                    Category = "SISTEMA"
                },
                new AlertTemplate
                {
                    Name = "Horário de Expediente",
                    Description = "Dispara apenas em horário comercial",
                    Condition = "current_hour >= 8 AND current_hour <= 18",
                    Category = "TEMPORAL"
                },
                new AlertTemplate
                {
                    Name = "Lote Falhado",
                    Description = "Dispara quando há lotes com falha",
                    Condition = "failed_batches_today > 0",
                    Category = "ERRO"
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
