using ClassificadorDoc.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
// using OpenAI; // Descomente quando usar OpenAI

var builder = WebApplication.CreateBuilder(args);

// Verificar modo de execução
var appMode = builder.Configuration["APP_MODE"] ?? Environment.GetEnvironmentVariable("APP_MODE") ?? "Full";

// Add services to the container.
builder.Services.AddControllers();

// Adicionar MVC apenas se não for modo "ApiOnly"
if (appMode != "ApiOnly")
{
    builder.Services.AddControllersWithViews();

    // Add Authentication apenas para interface web
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

    // Add Authorization
    builder.Services.AddAuthorization();
}

// Configure HttpClient for Gemini
builder.Services.AddHttpClient();

// OPÇÃO 1: Usando Gemini (ATIVO)
// Register services
builder.Services.AddScoped<IClassificadorService, GeminiClassificadorService>();

// OPÇÃO 2: Usando OpenAI (COMENTADO - descomente para usar OpenAI)
/*
using OpenAI;
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI API Key não configurada");
builder.Services.AddSingleton(new OpenAIClient(openAiApiKey));
builder.Services.AddScoped<IClassificadorService, ClassificadorService>();
*/

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register PdfExtractorService
builder.Services.AddScoped<PdfExtractorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Swagger apenas para modo API ou Full
    if (appMode != "WebOnly")
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}

app.UseHttpsRedirection();

// Static files apenas se não for modo "ApiOnly"
if (appMode != "ApiOnly")
{
    app.UseStaticFiles(); // Enable static files (CSS, JS, images)
}

app.UseRouting();

// Authentication/Authorization apenas se não for modo "ApiOnly"
if (appMode != "ApiOnly")
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Configure routing baseado no modo
if (appMode == "ApiOnly")
{
    // Apenas API controllers
    app.MapControllers();
}
else if (appMode == "WebOnly")
{
    // Apenas MVC routing
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
}
else // Full mode
{
    // Ambos MVC e API
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapControllers();
}

// Log do modo atual
app.Logger.LogInformation("Aplicação iniciada no modo: {AppMode}", appMode);

app.Run();
