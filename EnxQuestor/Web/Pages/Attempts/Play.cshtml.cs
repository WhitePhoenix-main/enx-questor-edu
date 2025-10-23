using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.DTO;
using Domain.Attempts;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Web.Identity;

[Authorize]
public sealed class PlayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IAttemptService _svc;
    private readonly UserManager<ApplicationUser> _userManager;
    public Domain.Attempts.Attempt Attempt { get; private set; } = default!;
    public List<Domain.Scenarios.ScenarioStep> Steps { get; private set; } = new();

    public PlayModel(AppDbContext db, IAttemptService svc, UserManager<ApplicationUser> userManager)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    // id может быть ИД попытки ИЛИ ИД сценария (трека)
    public async Task<IActionResult> OnGet(Guid id, CancellationToken ct)
    {
        var uid = _userManager.GetUserId(User);      // ← это значение из AspNetUsers.Id
        if (string.IsNullOrEmpty(uid)) return Challenge();
        // 1) Пытаемся загрузить попытку текущего пользователя
        Attempt = await _db.Attempts
            .Include(a => a.Steps)
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == uid, ct);

        if (Attempt is null)
        {
            // 2) Иначе трактуем id как ИД сценария и запускаем попытку (если нет активной)
            var scenario = await _db.Scenarios
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id, ct);
            if (scenario is null)
                return NotFound();

            var active = await _db.Attempts
                .AsNoTracking()
                .AnyAsync(a => a.UserId == uid && a.ScenarioId == id && a.Status == AttemptStatus.InProgress, ct);

            if (active)
            {
                // найдём её id и перейдём на неё
                var existing = await _db.Attempts
                    .Where(a => a.UserId == uid && a.ScenarioId == id && a.Status == AttemptStatus.InProgress)
                    .Select(a => a.Id)
                    .FirstAsync(ct);

                return RedirectToPage(new { id = existing });
            }

            // список шагов сценария в порядке выполнения
            var stepIds = await _db.ScenarioSteps
                .Where(s => s.ScenarioId == id)
                .OrderBy(s => s.Order)
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (stepIds.Count == 0)
                return BadRequest("У сценария нет шагов.");

            // доменная фабрика — создаём корректную попытку со списком AttemptStep
            var attempt = Domain.Attempts.Attempt.Start(id, uid, stepIds);

            _db.Attempts.Add(attempt);
            await _db.SaveChangesAsync(ct);

            return RedirectToPage(new { id = attempt.Id });
        }

        // Загрузка «визуальных» шагов по StepId из попытки (порядок из сценария)
        var stepIdsOrdered = await _db.ScenarioSteps
            .Where(s => s.ScenarioId == Attempt.ScenarioId)
            .OrderBy(s => s.Order)
            .Select(s => s.Id)
            .ToListAsync(ct);

        Steps = await _db.ScenarioSteps
            .Where(s => stepIdsOrdered.Contains(s.Id))
            .OrderBy(s => s.Order)
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnPostAnswer(Guid id, Guid stepId, string answerJson, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ok = await _db.Attempts.AnyAsync(a => a.Id == id && a.UserId == uid && a.Status == AttemptStatus.InProgress, ct);
        if (!ok) return Forbid();

        await _svc.AnswerAsync(uid, new AnswerRequest(id, stepId, answerJson), ct);
        TempData["Toast"] = "Ответ сохранён";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFinish(Guid id, CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ok = await _db.Attempts.AnyAsync(a => a.Id == id && a.UserId == uid && a.Status == AttemptStatus.InProgress, ct);
        if (!ok) return Forbid();

        await _svc.FinishAsync(uid, new FinishAttemptRequest(id), ct);
        TempData["Toast"] = "Попытка завершена. Достижения начисляются...";
        return RedirectToPage("/Attempts/Review", new { id });
    }
}
