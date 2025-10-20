using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Пытаемся прочитать appsettings из текущего каталога,
        // а если его нет — из проекта Web
        var basePath = Directory.GetCurrentDirectory();
        var builder = new ConfigurationBuilder().SetBasePath(basePath);

        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
            builder.SetBasePath(Path.Combine(basePath, "..", "Web"));

        var cfg = builder
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = cfg.GetConnectionString("Default")
                   ?? "Host=localhost;Database=eduplay;Username=postgres;Password=postgres";

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn, b => b.MigrationsHistoryTable("__EFMigrationsHistory_App"))
            .Options;

        return new AppDbContext(opts);
    }
}