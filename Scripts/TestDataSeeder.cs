using Microsoft.EntityFrameworkCore;
using ClassificadorDoc.Data;
using ClassificadorDoc.Models;

namespace ClassificadorDoc.Scripts
{
    public static class TestDataSeeder
    {
        public static async Task SeedTestProductivityData(ApplicationDbContext context)
        {
            var today = DateTime.Today;
            var users = await context.Users.Take(3).ToListAsync();

            if (!users.Any()) return;

            // Dados de atividade da plataforma para usuários
            foreach (var user in users)
            {
                var existingActivity = await context.UserProductivities
                    .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Date.Date == today);

                if (existingActivity == null)
                {
                    var productivity = new UserProductivity
                    {
                        UserId = user.Id,
                        Date = today,
                        LoginCount = Random.Shared.Next(1, 5),
                        TotalTimeOnline = TimeSpan.FromHours(Random.Shared.Next(2, 8) + Random.Shared.NextDouble()),
                        PagesAccessed = Random.Shared.Next(10, 50),
                        FirstLogin = today.AddHours(Random.Shared.Next(8, 10)),
                        LastActivity = DateTime.Now
                    };

                    context.UserProductivities.Add(productivity);
                }
            }

            // Dados de processamento de lotes
            for (int i = 0; i < 3; i++)
            {
                var user = users[i % users.Count];

                var batch = new BatchProcessingHistory
                {
                    UserId = user.Id,
                    UserName = user.FullName ?? user.UserName ?? "Usuário Teste",
                    BatchName = $"Lote Teste {i + 1} - {today:dd/MM}",
                    StartedAt = today.AddHours(9 + i * 2).AddMinutes(Random.Shared.Next(0, 60)),
                    CompletedAt = today.AddHours(9 + i * 2).AddMinutes(Random.Shared.Next(60, 120)),
                    TotalDocuments = Random.Shared.Next(20, 100),
                    ProcessingDuration = TimeSpan.FromMinutes(Random.Shared.Next(10, 45))
                };

                batch.SuccessfulDocuments = (int)(batch.TotalDocuments * (0.8 + Random.Shared.NextDouble() * 0.2));
                batch.FailedDocuments = batch.TotalDocuments - batch.SuccessfulDocuments;
                batch.AverageConfidence = 70 + Random.Shared.NextDouble() * 25; // 70-95%

                context.BatchProcessingHistories.Add(batch);
                await context.SaveChangesAsync(); // Salvar para obter o ID

                // Adicionar alguns documentos individuais para o lote
                for (int doc = 0; doc < Math.Min(batch.TotalDocuments, 5); doc++)
                {
                    var document = new DocumentProcessingHistory
                    {
                        FileName = $"documento_{doc + 1}.pdf",
                        ProcessedAt = batch.StartedAt.AddMinutes(doc * 2),
                        DocumentType = doc < batch.SuccessfulDocuments ? "autuacao" : "erro",
                        Confidence = Random.Shared.NextDouble() * 100,
                        UserId = user.Id,
                        IsSuccessful = doc < batch.SuccessfulDocuments,
                        BatchProcessingHistoryId = batch.Id,
                        FileSizeBytes = Random.Shared.Next(50000, 500000)
                    };

                    context.DocumentProcessingHistories.Add(document);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}