using Application.Abstractions;
using Application.Services;
using Infrastructure.Events;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Telegram;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Telegram options
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));

// EF Core + DbContext
var conn = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

// ВАЖНО: даём возможность инжектить DbContext там, где он ожидается
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = false;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.Password.RequireDigit = false;
    o.Password.RequiredLength = 6;
    o.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue<bool>("Auth:RequireConfirmedEmail");
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Приложение/доменные сервисы
builder.Services.AddScoped<IAttemptService, AttemptService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<Domain.Common.IDomainEventPublisher, DomainEventPublisher>();

// Telegram
builder.Services.AddHttpClient("tg");
builder.Services.AddScoped<ITelegramBotClient>(sp =>
{
    var opts = sp.GetRequiredService<IConfiguration>()
                 .GetSection("Telegram")
                 .Get<TelegramOptions>()!;
    return new TelegramBotClient(opts.BotToken);
});
builder.Services.AddScoped<ITelegramWebhookHandler, TelegramWebhookHandler>();

// Razor Pages + AuthZ
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
})
.AddRazorRuntimeCompilation();

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    opts.AddPolicy("TeacherOnly", p => p.RequireRole("Teacher", "Admin"));
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen((SwaggerGenOptions c) =>
{
    c.SwaggerDoc("v1", new() { Title = "EduPlay API", Version = "v1" });
});

var app = builder.Build();

// Сиды БД
using (var scope = app.Services.CreateScope())
{
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Minimal APIs
Web.Api.ScenariosApi.Map(app);
Web.Api.AttemptsApi.Map(app);
Web.Api.AdminApi.Map(app);
Web.Bot.WebhookEndpoints.Map(app);

app.Run();
