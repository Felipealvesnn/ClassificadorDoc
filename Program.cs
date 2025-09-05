using ClassificadorDoc.Services;
using ClassificadorDoc.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
// using OpenAI; // Descomente quando usar OpenAI

var builder = WebApplication.CreateBuilder(args);

// Verificar modo de execução
var appMode = builder.Configuration["APP_MODE"] ?? Environment.GetEnvironmentVariable("APP_MODE") ?? "Full";

// Configurar Entity Framework e Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar Identity apenas se não for modo "ApiOnly"
if (appMode != "ApiOnly")
{
    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // Configurações de senha
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        // Configurações de conta
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        // Configurações de lockout
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // Configurar cookie de autenticação
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
}

// Add services to the container.
builder.Services.AddControllers();

// Adicionar MVC apenas se não for modo "ApiOnly"
if (appMode != "ApiOnly")
{
    builder.Services.AddControllersWithViews();

    // Add Authorization com políticas
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin", "Classifier"));
        options.AddPolicy("ClassifierOrAdmin", policy => policy.RequireRole("Classifier", "Admin"));
    });
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

// Register Alert Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAlertConditionEngine, AlertConditionEngine>();
builder.Services.AddScoped<IAlertExecutionService, AlertExecutionService>();
builder.Services.AddScoped<ISystemNotificationService, SystemNotificationService>();

// Configure SignalR for real-time notifications
builder.Services.AddSignalR();

// Configure Quartz.NET for automatic alert execution
builder.Services.AddQuartz(q =>
{
    // Configure job to run every 5 minutes
    var jobKey = new JobKey("AlertExecutionJob");
    q.AddJob<AlertExecutionJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("AlertExecutionTrigger")
        .WithCronSchedule("0 */5 * * * ?")); // Every 5 minutes
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

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

// Seed inicial de dados (apenas se não for ApiOnly)
if (appMode != "ApiOnly")
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            await SeedData.InitializeAsync(scope.ServiceProvider);
            app.Logger.LogInformation("Seed de dados executado com sucesso");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Erro ao executar seed de dados");
        }
    }
}

app.Run();
