using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Achievements;
using Domain.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public static class DataSeeder
{
    /// <summary>
    /// Сидит доменные данные. Identity-юзеров/ролей НЕ трогаем.
    /// teacherId — Id пользователя-преподавателя, которому будут принадлежать сценарии.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider sp, string teacherId, CancellationToken ct = default)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);

        if (!await db.Scenarios.AnyAsync(ct))
        {
            var sc1 = Scenario.Create(
                "Введение в SOLID", "solid-intro", teacherId, 1,
                "Теория + проверка ключевых принципов.", "oop,solid");

            var sc1_s1 = JsonSerializer.Serialize(new { md = "## Принципы SOLID" });
            var sc1_s2 = JsonSerializer.Serialize(new
            {
                question = "Что означает S?",
                options = new[] { "Single", "Simple", "Solid" },
                correct = "Single"
            });
            var sc1_s3 = JsonSerializer.Serialize(new
            {
                prompt = "Опишите SRP",
                keywords = new[] { "responsibility", "ответственность" }
            });
            var sc1_s4 = JsonSerializer.Serialize(new
            {
                question = "Выберите принципы",
                options = new[] { "SRP", "DRY", "LSP" },
                correct = new[] { "SRP", "LSP" }
            });

            sc1.SetSteps(new[]
            {
                ScenarioStep.Create(sc1.Id, 1, ScenarioStepType.Text,        sc1_s1, 0, "Теория"),
                ScenarioStep.Create(sc1.Id, 2, ScenarioStepType.Single,      sc1_s2, 5, "Single"),
                ScenarioStep.Create(sc1.Id, 3, ScenarioStepType.ShortAnswer, sc1_s3, 5, "SRP"),
                ScenarioStep.Create(sc1.Id, 4, ScenarioStepType.Multi,       sc1_s4, 5, "Принципы"),
            });
            sc1.Publish(true);

            var sc2 = Scenario.Create(
                "Тест по Git", "git-quiz", teacherId, 1,
                "Быстрый квиз по Git.", "git");

            var sc2_s1 = JsonSerializer.Serialize(new
            {
                question = "Команда для коммита?",
                options = new[] { "git push", "git commit", "git log" },
                correct = "git commit"
            });
            var sc2_s2 = JsonSerializer.Serialize(new
            {
                prompt = "Как создать ветку?",
                keywords = new[] { "git", "branch" }
            });

            sc2.SetSteps(new[]
            {
                ScenarioStep.Create(sc2.Id, 1, ScenarioStepType.Single,      sc2_s1, 5, "Commit"),
                ScenarioStep.Create(sc2.Id, 2, ScenarioStepType.ShortAnswer, sc2_s2, 5, "Branch"),
            });
            sc2.Publish(true);

            db.Scenarios.AddRange(sc1, sc2);
        }

        if (!await db.Achievements.AnyAsync(ct))
        {
            var a1 = Achievement.Create(
                "first_complete", "Первый шаг",
                JsonSerializer.Serialize(new { type = "FirstCompletion" }),
                iconUrl: null,
                desc: "Первое завершение сценария");

            var a2 = Achievement.Create(
                "three_completes", "Три в ряд",
                JsonSerializer.Serialize(new { type = "CompleteScenarios", count = 3 }),
                iconUrl: null,
                desc: "Завершите 3 сценария");

            var a3 = Achievement.Create(
                "perfect_test", "Идеал",
                JsonSerializer.Serialize(new { type = "PerfectTest" }),
                iconUrl: null,
                desc: "Пройдите тест без ошибок");

            db.Achievements.AddRange(a1, a2, a3);
        }

        await db.SaveChangesAsync(ct);
    }
}
