using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Web.Identity;

public sealed class AppIdentityDbContextFactory : IDesignTimeDbContextFactory<AppIdentityDbContext>
{
    public AppIdentityDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var builder = new ConfigurationBuilder().SetBasePath(basePath);

        // для запуска из корня/Infrastructure подхватим конфиг из Web
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
            builder.SetBasePath(Path.Combine(basePath, "..", "Web"));

        var cfg = builder
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = cfg.GetConnectionString("Default")
                   ?? "Host=localhost;Database=eduplay;Username=postgres;Password=postgres";

        var opts = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseNpgsql(conn, b => b.MigrationsHistoryTable("__EFMigrationsHistory_Identity"))
            .Options;

        return new AppIdentityDbContext(opts);
    }
}