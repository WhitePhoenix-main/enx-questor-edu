
using Domain.Achievements;
using Domain.Attempts;
using Domain.Scenarios;
using Domain.Telegram;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Identity;
namespace Infrastructure.Persistence;
public sealed class AppDbContext : IdentityDbContext<AppUser>
{
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<ScenarioStep> ScenarioSteps => Set<ScenarioStep>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptStep> AttemptSteps => Set<AttemptStep>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<Domain.Achievements.UserAchievement> UserAchievements => Set<Domain.Achievements.UserAchievement>();
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
    public DbSet<BotUpdateLog> BotUpdateLogs => Set<BotUpdateLog>();
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasDefaultSchema("public");
        b.Entity<Scenario>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Tags).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasMany<ScenarioStep>().WithOne().HasForeignKey(x => x.ScenarioId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ScenarioStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(4000);
            e.Property(x => x.StepType).HasConversion<int>();
        });
        b.Entity<Attempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasMany<AttemptStep>().WithOne().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AttemptStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AnswerJson).HasMaxLength(4000);
        });
        b.Entity<Achievement>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.RuleJson).HasMaxLength(2000);
        });
        b.Entity<Domain.Achievements.UserAchievement>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.AchievementId }).IsUnique();
        });
        b.Entity<TelegramLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OneTimeCode).IsUnique();
        });
        b.Entity<BotUpdateLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UpdateId).IsUnique();
            e.Property(x => x.PayloadJson).HasMaxLength(4000);
        });
    }
}
