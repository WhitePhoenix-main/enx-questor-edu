// Infrastructure.Persistence/Configurations/AttemptConfiguration.cs
using Domain.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.UserId).IsRequired();
        b.Property(a => a.Status).IsRequired();
        b.Property(a => a.StartedAt).IsRequired();

        // Навигация Steps использует бэкинг-поле _steps
        b.Metadata.FindNavigation(nameof(Attempt.Steps))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany<AttemptStep>("_steps")
            .WithOne()
            .HasForeignKey(s => s.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// Аналогично — AttemptStepConfiguration (минимально):
public sealed class AttemptStepConfiguration : IEntityTypeConfiguration<AttemptStep>
{
    public void Configure(EntityTypeBuilder<AttemptStep> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.StepId).IsRequired();
        b.Property(s => s.AttemptId).IsRequired();
        // при наличии статусов/оценок — пометьте как Required/HasDefaultValue
    }
}