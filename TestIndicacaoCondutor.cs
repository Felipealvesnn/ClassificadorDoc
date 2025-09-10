using ClassificadorDoc.Services;
using ClassificadorDoc.Models;
using System.Text.Json;

namespace ClassificadorDoc
{
    /// <summary>
    /// Classe de teste para verificar a funcionalidade de extração de dados
    /// de indicação de condutor com CPF e RG
    /// </summary>
    public static class TestIndicacaoCondutor
    {
        public static async Task TestarExtracao()
        {
            // Simular um texto de indicação de condutor
            var textoIndicacao = @"
PREFEITURA MUNICIPAL DE SÃO PAULO
SECRETARIA MUNICIPAL DE TRANSPORTES
FORMULÁRIO DE INDICAÇÃO DE CONDUTOR

DADOS DO REQUERENTE (PROPRIETÁRIO DO VEÍCULO):
Nome: João Silva Santos
CPF: 123.456.789-10
RG: 12.345.678-9 SSP/SP
Endereço: Rua das Flores, 123 - Centro - São Paulo/SP

DADOS DA INDICAÇÃO (CONDUTOR DA INFRAÇÃO):
Nome: Maria Oliveira Costa
CPF: 987.654.321-00
RG: 98.765.432-1 SSP/SP
CNH: 12345678901

DADOS DA INFRAÇÃO:
Número do AIT: 123456789
Placa do Veículo: ABC-1234
Data da Infração: 15/03/2023
Local: Av. Paulista, 1000 - Bela Vista
Código da Infração: 554-20
Valor da Multa: R$ 195,23
Órgão Autuador: DSV - Departamento do Sistema Viário

Declaro que o condutor acima identificado estava dirigindo o veículo no momento da infração.
";

            Console.WriteLine("=== TESTE DE EXTRAÇÃO - INDICAÇÃO DE CONDUTOR ===");
            Console.WriteLine();
            Console.WriteLine("Texto simulado:");
            Console.WriteLine(textoIndicacao);
            Console.WriteLine();
            Console.WriteLine("=== RESULTADO ESPERADO ===");
            Console.WriteLine("✅ Tipo: indicacao_condutor");
            Console.WriteLine("✅ Requerente Nome: João Silva Santos");
            Console.WriteLine("✅ Requerente CPF: 123.456.789-10");
            Console.WriteLine("✅ Requerente RG: 12.345.678-9");
            Console.WriteLine("✅ Indicação Nome: Maria Oliveira Costa");
            Console.WriteLine("✅ Indicação CPF: 987.654.321-00");
            Console.WriteLine("✅ Indicação RG: 98.765.432-1");
            Console.WriteLine("✅ Indicação CNH: 12345678901");
            Console.WriteLine("✅ Número AIT: 123456789");
            Console.WriteLine("✅ Placa: ABC-1234");
            Console.WriteLine();
            Console.WriteLine("=== INSTRUÇÕES PARA TESTE MANUAL ===");
            Console.WriteLine("1. Acesse https://localhost:5001");
            Console.WriteLine("2. Faça login no sistema");
            Console.WriteLine("3. Vá para a página de Upload de Documentos");
            Console.WriteLine("4. Crie um PDF com o texto acima");
            Console.WriteLine("5. Coloque o PDF em um arquivo ZIP");
            Console.WriteLine("6. Faça upload do ZIP");
            Console.WriteLine("7. Verifique se os dados foram extraídos corretamente");
            Console.WriteLine("8. Acesse o histórico para ver os detalhes dos campos extraídos");
        }
    }
}
