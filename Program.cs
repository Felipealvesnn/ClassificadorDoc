using ClassificadorDoc.Services;
// using OpenAI; // Descomente quando usar OpenAI

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure HttpClient for Gemini
builder.Services.AddHttpClient();

// OPÇÃO 1: Usando Gemini (ATIVO)
// Register services
builder.Services.AddScoped<IPdfExtractorService, PdfExtractorService>();
builder.Services.AddScoped<IClassificadorService, GeminiClassificadorService>();

// OPÇÃO 2: Usando OpenAI (COMENTADO - descomente para usar OpenAI)
/*
using OpenAI;
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI API Key não configurada");
builder.Services.AddSingleton(new OpenAIClient(openAiApiKey));
builder.Services.AddScoped<IPdfExtractorService, PdfExtractorService>();
builder.Services.AddScoped<IClassificadorService, ClassificadorService>();
*/

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
