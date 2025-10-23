using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.DTO;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Web.Identity;

namespace Web.Pages.Attempts;

[Authorize]
public sealed class PlayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IAttemptService _svc;
    private readonly UserManager<ApplicationUser> _userManager;

    public Domain.Attempts.Attempt Attempt { get; private set; } = default!;
    public List<Domain.Scenarios.ScenarioStep> Steps { get; private set; } = new();

    // Прогресс/состояние UI
    public int TotalSteps { get; private set; }
    public int CurrentStepNumber { get; private set; } // 1-based
    public bool AllStepsAnswered { get; private set; }
    public Guid CurrentAttemptStepId { get; private set; } // справочно (в формах не нужен)

    public PlayModel(AppDbContext db, IAttemptService svc, UserManager<ApplicationUser> userManager)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    private static bool HasRealAnswer(Domain.Attempts.AttemptStep st)
    {
        var a = st.AnswerJson;
        if (string.IsNullOrWhiteSpace(a)) return false;
        a = a.Trim();
        return a != "{}" && a != "null";
    }

    // id = AttemptId ИЛИ ScenarioId, n = номер шага (1-based, опционально)
    public async Task<IActionResult> OnGet(Guid id, int? n, CancellationToken ct)
    {
        var uid = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(uid)) return Challenge();

        Attempt = await _db.Attempts
            .Include(a => a.Steps)
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == uid, ct);

        if (Attempt is null)
        {
            // трактуем id как ScenarioId → создаём попытку и уводим на шаг 1
            var scenario = await _db.Scenarios.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
            if (scenario is null) return NotFound();

            var active = await _db.Attempts.AsNoTracking()
                .AnyAsync(a => a.UserId == uid && a.ScenarioId == id && a.Status == Domain.Attempts.AttemptStatus.InProgress, ct);

            if (active)
            {
                var existing = await _db.Attempts
                    .Where(a => a.UserId == uid && a.ScenarioId == id && a.Status == Domain.Attempts.AttemptStatus.InProgress)
                    .Select(a => a.Id)
                    .FirstAsync(ct);

                return RedirectToPage(new { id = existing, n = 1 });
            }

            var stepIds = await _db.ScenarioSteps
                .Where(s => s.ScenarioId == id)
                .OrderBy(s => s.Order)
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (stepIds.Count == 0) return BadRequest("У сценария нет шагов.");

            var attempt = Domain.Attempts.Attempt.Start(id, uid, stepIds);
            _db.Attempts.Add(attempt);
            await _db.SaveChangesAsync(ct);

            return RedirectToPage(new { id = attempt.Id, n = 1 });
        }

        if (Attempt.Status == Domain.Attempts.AttemptStatus.Completed)
            return RedirectToPage("/Attempts/Review", new { id = Attempt.Id });

        // Текущий порядок шагов сценария
        var scenarioStepIds = await _db.ScenarioSteps
            .Where(s => s.ScenarioId == Attempt.ScenarioId)
            .OrderBy(s => s.Order)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (scenarioStepIds.Count == 0) return BadRequest("У сценария нет шагов.");

        // Карта порядка
        var orderMap = scenarioStepIds
            .Select((sid, idx) => new { sid, idx })
            .ToDictionary(x => x.sid, x => x.idx);

        // AttemptSteps в порядке сценария (фильтруем только те, что есть в сценарии)
        var orderedAttemptSteps = Attempt.Steps
            .Where(st => orderMap.ContainsKey(st.StepId))
            .OrderBy(st => orderMap[st.StepId])
            .ToList();

        // Если сценарий меняли и по текущему сценарию ничего не осталось
        if (orderedAttemptSteps.Count == 0)
        {
            AllStepsAnswered = true;
            Steps = new();
            CurrentStepNumber = 0;
            TotalSteps = 0;
            return Page();
        }

        // Прогресс считаем по фактическим шагам попытки (устойчиво к изменениям сценария)
        TotalSteps = orderedAttemptSteps.Count;
        var firstUnansweredIdx = orderedAttemptSteps.FindIndex(st => !HasRealAnswer(st)); // 0-based
        AllStepsAnswered = firstUnansweredIdx == -1;

        // Если n не задан — ведём на первый неотвеченный, иначе показываем "всё пройдено"
        if (!n.HasValue)
        {
            if (AllStepsAnswered)
            {
                Steps = new();
                CurrentStepNumber = TotalSteps;
                return Page();
            }
            return RedirectToPage(new { id = Attempt.Id, n = firstUnansweredIdx + 1 });
        }

        // Нормализуем n по фактическим шагам попытки
        var requestedIdx = Math.Clamp(n.Value, 1, TotalSteps) - 1;

        // Не даём прыгать вперёд дальше первого неотвеченного
        if (!AllStepsAnswered && requestedIdx > firstUnansweredIdx)
            return RedirectToPage(new { id = Attempt.Id, n = firstUnansweredIdx + 1 });

        // Берём AttemptStep по индексу и из него — ScenarioStepId для визуализации
        var attemptStep = orderedAttemptSteps[requestedIdx];
        CurrentAttemptStepId = attemptStep.Id;
        CurrentStepNumber = requestedIdx + 1;

        Steps = await _db.ScenarioSteps
            .Where(s => s.Id == attemptStep.StepId)
            .ToListAsync(ct);

        return Page();
    }

    // POST: сохраняем ответ по номеру шага n → в сервис уходит ScenarioStep.Id
    public async Task<IActionResult> OnPostAnswer(Guid id, int n, string answerJson, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Валидируем попытку
        var attempt = await _db.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == uid && a.Status == Domain.Attempts.AttemptStatus.InProgress, ct);
        if (attempt is null) return Forbid();

        // Определяем ScenarioStep.Id шага № n (1-based) по порядку сценария
        var scenarioStepIds = await _db.ScenarioSteps
            .Where(ss => ss.ScenarioId == attempt.ScenarioId)
            .OrderBy(ss => ss.Order)
            .Select(ss => ss.Id)
            .ToListAsync(ct);

        if (scenarioStepIds.Count == 0) return BadRequest("У сценария нет шагов.");

        var idx = Math.Clamp(n, 1, scenarioStepIds.Count) - 1;
        var scenarioStepId = scenarioStepIds[idx];

        // Сохраняем ответ: сервис ожидает ScenarioStep.Id
        await _svc.AnswerAsync(uid, new AnswerRequest(id, scenarioStepId, answerJson), ct);

        // Навигация вперёд
        var nextN = n + 1;
        if (nextN > scenarioStepIds.Count)
            return RedirectToPage(new { id }); // без n → OnGet покажет «Все шаги пройдены»

        return RedirectToPage(new { id, n = nextN });
    }

    public async Task<IActionResult> OnPostFinish(Guid id, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ok = await _db.Attempts
            .AnyAsync(a => a.Id == id && a.UserId == uid && a.Status == Domain.Attempts.AttemptStatus.InProgress, ct);
        if (!ok) return Forbid();

        await _svc.FinishAsync(uid, new FinishAttemptRequest(id), ct);
        TempData["Toast"] = "Попытка завершена. ДAchievements начисляются...";
        return RedirectToPage("/Attempts/Review", new { id });
    }
}
