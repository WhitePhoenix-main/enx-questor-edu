
using System;
using System.Collections.Generic;
using System.Linq;
using Application.Abstractions;
using Application.DTO;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize]
public sealed class PlayModel : PageModel
{
    private readonly AppDbContext _db; private readonly IAttemptService _svc;
    public Domain.Attempts.Attempt Attempt { get; private set; } = default!;
    public List<Domain.Scenarios.ScenarioStep> Steps { get; private set; } = new();
    public PlayModel(AppDbContext db, IAttemptService svc) { _db = db; _svc = svc; }
    public async Task<IActionResult> OnGet(Guid id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Attempt = await _db.Attempts.Include(a=>a.Steps).FirstAsync(a=>a.Id==id && a.UserId==uid);
        var stepIds = Attempt.Steps.Select(s=>s.StepId).ToList();
        Steps = await _db.ScenarioSteps.Where(s=> stepIds.Contains(s.Id)).OrderBy(s=>s.Order).ToListAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostAnswer(Guid id, Guid stepId, string answerJson)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _svc.AnswerAsync(uid, new AnswerRequest(id, stepId, answerJson), HttpContext.RequestAborted);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostFinish(Guid id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _svc.FinishAsync(uid, new FinishAttemptRequest(id), HttpContext.RequestAborted);
        TempData["Toast"] = "Попытка завершена. Достижения начисляются...";
        return RedirectToPage("/Attempts/Review", new { id });
    }
}
