using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Domain.Attempts;
using Domain.Scenarios;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Web.Pages.Attempts
{
    [Authorize]
    public class ReviewModel : PageModel
    {
        private readonly AppDbContext _db;
        public ReviewModel(AppDbContext db) => _db = db;

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        public AttemptReviewVm? Data { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            // 1) Загружаем попытку
            var attempt = await _db.Attempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == Id, ct);

            if (attempt is null)
                return NotFound();

            // 2) Проверка доступа: владелец (если есть UserId) или Admin
            var attemptUserId = TryGet<string>(attempt, "UserId") ?? TryGet<string>(attempt, "OwnerId");
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!string.IsNullOrEmpty(attemptUserId))
            {
                var isOwner = !string.IsNullOrEmpty(currentUserId) && attemptUserId == currentUserId;
                if (!isOwner && !isAdmin)
                    return Forbid();
            }
            // если в модели нет поля пользователя — не блокируем (оставляем только авторизацию атрибутом)

            // 3) Пытаемся найти сценарий (по ScenarioId или навигации Scenario)
            Guid? scenarioId = TryGet<Guid?>(attempt, "ScenarioId");
            Scenario? scenario = null;
            if (scenarioId is Guid sid && sid != Guid.Empty)
                scenario = await _db.Scenarios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct);
            else
                scenario = TryGet<Scenario>(attempt, "Scenario"); // если у Attempt есть навигация

            // 4) Загружаем шаги попытки (сырьё для ответов)
            var steps = await _db.AttemptSteps
                .AsNoTracking()
                .Where(s => s.AttemptId == attempt.Id)
                .ToListAsync(ct);

            // Если можем связать шаги сценария — подтянем их для заголовков
            Dictionary<Guid, string>? scenarioStepTitles = null;
            var scenarioStepIdProp = steps.FirstOrDefault()?.GetType().GetProperty("ScenarioStepId");
            if (scenario is not null && scenarioStepIdProp is not null)
            {
                var scenarioStepIds = steps
                    .Select(s => (Guid?)scenarioStepIdProp.GetValue(s) ?? Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToArray();

                if (scenarioStepIds.Length > 0)
                {
                    var ssteps = await _db.ScenarioSteps
                        .AsNoTracking()
                        .Where(x => scenarioStepIds.Contains(x.Id))
                        .Select(x => new { x.Id, x.Content })
                        .ToListAsync(ct);

                    scenarioStepTitles = ssteps.ToDictionary(x => x.Id, x => (x.Content ?? "").Trim());
                }
            }

            // 5) Считаем базовые метрики
            var startedUtc   = TryGet<DateTime?>(attempt, "StartedAt") ?? TryGet<DateTime?>(attempt, "StartedUtc");
            var finishedUtc  = TryGet<DateTime?>(attempt, "FinishedAt") ?? TryGet<DateTime?>(attempt, "CompletedAt");
            var score        = TryGet<int?>(attempt, "Score") ?? TryGet<int?>(attempt, "Points") ?? 0;
            var maxScore     = TryGet<int?>(attempt, "MaxScore") ?? TryGet<int?>(attempt, "MaxPoints") 
                               ?? Math.Max(1, steps.Count);
            var correctCount = TryGet<int?>(attempt, "CorrectCount") ?? 0;
            var incorrectCnt = TryGet<int?>(attempt, "IncorrectCount") ?? 0;
            var passFlag     = TryGet<bool?>(attempt, "Passed") ?? null;

            double? percent = (maxScore > 0) ? (score * 100.0 / maxScore) : null;
            int? passThreshold = TryGet<int?>(scenario, "PassThresholdPercent");

            bool passed = passFlag ?? (percent is not null && passThreshold is not null && percent >= passThreshold);

            // 6) Собираем VM ответов
            var answers = new List<AnswerVm>();
            int index = 1;

            // попытаемся отсортировать по полю Order/Index, иначе по Id
            var orderedSteps = steps
                .Select(s => new
                {
                    Step = s,
                    Order = TryGet<int?>(s, "Order") ?? TryGet<int?>(s, "Index"),
                    Id = TryGet<Guid?>(s, "Id") ?? Guid.Empty
                })
                .OrderBy(x => x.Order ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .Select(x => x.Step);

            foreach (var st in orderedSteps)
            {
                var isCorrect    = TryGet<bool?>(st, "IsCorrect");
                var points       = TryGet<int?>(st, "PointsAwarded") ?? TryGet<int?>(st, "Points") ?? 0;
                var secs         = TryGet<int?>(st, "TimeSpentSec") ?? TryGet<int?>(st, "Seconds");
                var usedHint     = TryGet<bool?>(st, "UsedHint");
                var answerJson   = TryGet<string>(st, "AnswerJson") ?? "";

                string? stepTitle = null;
                if (scenarioStepTitles is not null && scenarioStepIdProp is not null)
                {
                    var sid1 = (Guid?)scenarioStepIdProp.GetValue(st);
                    if (sid1 is Guid k && scenarioStepTitles.TryGetValue(k, out var title))
                        stepTitle = title;
                }

                answers.Add(new AnswerVm
                {
                    Index = index++,
                    StepTitle = stepTitle,
                    IsCorrect = isCorrect,
                    Points = points,
                    TimeSpentSec = secs,
                    UsedHint = usedHint,
                    AnswerJson = answerJson
                });
            }

            // Если в попытке нет готовых счётчиков верных/неверных — оценим по шагам
            if (correctCount == 0 && incorrectCnt == 0 && answers.Count > 0)
            {
                correctCount  = answers.Count(a => a.IsCorrect == true);
                incorrectCnt  = answers.Count(a => a.IsCorrect == false);
                maxScore      = Math.Max(maxScore, answers.Count); // безопасная верхняя граница
                percent       = (maxScore > 0) ? (score * 100.0 / maxScore) : percent;
            }

            // 7) Итоговая VM
            Data = new AttemptReviewVm
            {
                AttemptId = attempt.Id,
                ScenarioId = scenario?.Id ?? scenarioId ?? Guid.Empty,
                ScenarioTitle = TryGet<string>(scenario, "Title") ?? "",
                ScenarioSlug = TryGet<string>(scenario, "Slug") ?? "",
                UserDisplayName = TryGet<string>(attempt, "UserDisplayName") ?? TryGet<string>(attempt, "UserName") ?? "Вы",
                StartedAtLocal = ToLocal(startedUtc),
                FinishedAtLocal = ToLocal(finishedUtc),
                DurationHuman = FormatDuration(startedUtc, finishedUtc),
                Score = score,
                MaxScore = maxScore,
                Percent = percent,
                PassThresholdPercent = passThreshold,
                Correct = correctCount,
                Incorrect = incorrectCnt,
                TotalSteps = steps.Count,
                Passed = passed,
                ShareUrl = TryGet<string>(attempt, "PublicShareToken") is string token && !string.IsNullOrWhiteSpace(token)
                    ? $"/Attempts/Share/{token}"
                    : null,
                Answers = answers
            };

            return Page();
        }

        // ===== helpers =====

        private static T? TryGet<T>(object? obj, string prop)
        {
            if (obj is null) return default;

            var p = obj.GetType().GetProperty(prop);
            if (p is null) return default;

            var val = p.GetValue(obj);
            if (val is null) return default;

            var targetType = typeof(T);
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // --- корректная обработка дат/времени ---
            if (underlying == typeof(DateTime))
            {
                if (val is DateTimeOffset dto)
                {
                    var dtUtc = dto.UtcDateTime; // нормализуем к UTC
                    return (T)(object)dtUtc;
                }
                if (val is DateTime dt)
                {
                    DateTime dtUtc = dt.Kind switch
                    {
                        DateTimeKind.Utc => dt,
                        DateTimeKind.Local => dt.ToUniversalTime(),
                        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    };
                    return (T)(object)dtUtc;
                }
            }

            if (underlying == typeof(DateTimeOffset))
            {
                if (val is DateTime dt2)
                {
                    DateTime dtUtc = dt2.Kind switch
                    {
                        DateTimeKind.Utc => dt2,
                        DateTimeKind.Local => dt2.ToUniversalTime(),
                        _ => DateTime.SpecifyKind(dt2, DateTimeKind.Utc)
                    };
                    return (T)(object)new DateTimeOffset(dtUtc, TimeSpan.Zero);
                }
                if (val is DateTimeOffset dto2)
                {
                    return (T)(object)dto2;
                }
            }

            // Enum из строки/числа
            if (underlying.IsEnum)
            {
                try
                {
                    if (val is string es) return (T)Enum.Parse(underlying, es, ignoreCase: true);
                    if (val is IConvertible) return (T)Enum.ToObject(underlying, val);
                }
                catch { return default; }
            }

            // Если уже нужного типа
            if (val is T typed) return typed;

            // Универсальная конвертация для простых типов
            try
            {
                var converted = Convert.ChangeType(val, underlying);
                return (T)converted!;
            }
            catch
            {
                return default;
            }
        }

        private static DateTime? ToLocal(DateTime? utc)
        {
            if (utc is null) return null;
            // Здесь utc ГАРАНТИРОВАНО в UTC после TryGet<T>
            return TimeZoneInfo.ConvertTimeFromUtc(utc.Value, TimeZoneInfo.Local);
        }

        private static string FormatDuration(DateTime? startedUtc, DateTime? finishedUtc)
        {
            if (startedUtc is null || finishedUtc is null) return "—";
            var span = finishedUtc.Value - startedUtc.Value;
            if (span.TotalSeconds < 1) return "меньше 1 c";
            var parts = new List<string>();
            if (span.Hours > 0) parts.Add($"{span.Hours} ч");
            if (span.Minutes > 0) parts.Add($"{span.Minutes} м");
            parts.Add($"{span.Seconds} с");
            return string.Join(" ", parts);
        }

        // ===== VM =====
        public sealed class AttemptReviewVm
        {
            public Guid AttemptId { get; set; }
            public Guid ScenarioId { get; set; }
            public string ScenarioTitle { get; set; } = "";
            public string ScenarioSlug { get; set; } = "";
            public string UserDisplayName { get; set; } = "";
            public DateTime? StartedAtLocal { get; set; }
            public DateTime? FinishedAtLocal { get; set; }
            public string? DurationHuman { get; set; }
            public int? Score { get; set; }
            public int MaxScore { get; set; }
            public double? Percent { get; set; }
            public int? PassThresholdPercent { get; set; }
            public int Correct { get; set; }
            public int Incorrect { get; set; }
            public int TotalSteps { get; set; }
            public bool Passed { get; set; }
            public string? ShareUrl { get; set; }
            public List<AnswerVm> Answers { get; set; } = new();
        }

        public sealed class AnswerVm
        {
            public int Index { get; set; }
            public string? StepTitle { get; set; }
            public bool? IsCorrect { get; set; }
            public int Points { get; set; }
            public int? TimeSpentSec { get; set; }
            public bool? UsedHint { get; set; }
            public string AnswerJson { get; set; } = "";
        }
    }
}
