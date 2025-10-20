using System;
using System.Linq;
using Application.Abstractions;
using Application.Services;
using Infrastructure.Events;
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

using Web.Identity;                  // ApplicationUser, ApplicationRole, AppIdentityDbContext, IdentitySeeder
using Web.Authorization;             // CrudPolicies
using Infrastructure.Telegram;       // ITelegramWebhookHandler
using Web.Telegram;                  // TelegramWebhookHandler
using Microsoft.AspNetCore.Authentication;
using SystemClock = Application.Abstractions.SystemClock;

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

// --- БД прикладного уровня ---
var conn = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

// --- БД для ASP.NET Core Identity (ОТДЕЛЬНЫЙ контекст) ---
builder.Services.AddDbContext<AppIdentityDbContext>(o => o.UseNpgsql(conn));

// ❌ УДАЛЕНО: AddDefaultIdentity<ApplicationUser>(...).AddEntityFrameworkStores<AppIdentityDbContext>();

// --- Полноценная Identity с ролями + UI ---
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(o =>
    {
        o.User.RequireUniqueEmail = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireDigit = false;
        o.Password.RequiredLength = 6;
        o.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue<bool>("Auth:RequireConfirmedEmail");
    })
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI(); // чтобы работали страницы /Identity/Account/*

// Если где-то ожидается именно DbContext — маппим на доменный контекст
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

// Приложение/доменные сервисы
builder.Services.AddScoped<IAttemptService, AttemptService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<Domain.Common.IDomainEventPublisher, DomainEventPublisher>();

// Telegram
builder.Services.AddHttpClient("tg");
builder.Services.AddScoped<ITelegramBotClient>(sp =>
{
    var opts = sp.GetRequiredService<IConfiguration>().GetSection("Telegram").Get<TelegramOptions>()!;
    return new TelegramBotClient(opts.BotToken);
});
builder.Services.AddScoped<ITelegramWebhookHandler, TelegramWebhookHandler>();

// Razor Pages + AuthZ
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/Admin", CrudPolicies.Read);
    })
    .AddRazorRuntimeCompilation();

// Политики CRUD
builder.Services.AddCrudPolicies();

// Ролевые политики
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
    // 1) Роли + dev-админ
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);

    // 2) Гарантируем наличие Teacher и получаем его Id
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

    const string teacherEmail = "teacher@local";
    var teacher = await userMgr.FindByEmailAsync(teacherEmail);
    if (teacher is null)
    {
        if (await roleMgr.FindByNameAsync("Teacher") is null)
            await roleMgr.CreateAsync(new ApplicationRole { Name = "Teacher" });

        var u = new ApplicationUser
        {
            UserName = teacherEmail,
            Email = teacherEmail,
            EmailConfirmed = true,
        };
        var create = await userMgr.CreateAsync(u, "Passw0rd!");
        if (!create.Succeeded)
            throw new Exception("Failed to create teacher: " +
                                string.Join("; ", create.Errors.Select(e => e.Description)));

        await userMgr.AddToRoleAsync(u, "Teacher");
        teacher = u;
    }

    // 3) Доменные сиды: передаём teacher.Id
    await Infrastructure.Persistence.DataSeeder.SeedAsync(scope.ServiceProvider, teacher.Id);
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
