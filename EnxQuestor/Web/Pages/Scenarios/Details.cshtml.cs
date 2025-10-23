using System;
using System.Linq;
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
// если ApplicationUser в другом неймспейсе — поправь using ниже
using Web.Identity;

namespace Web.Pages.Scenarios;

[Authorize] // по умолчанию требуем логин
public sealed class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IAttemptService _attempts;
    private readonly UserManager<ApplicationUser> _userManager;

    public Domain.Scenarios.Scenario? Scenario { get; private set; }

    public DetailsModel(
        AppDbContext db,
        IAttemptService attempts,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _attempts = attempts;
        _userManager = userManager;
    }

    [AllowAnonymous] // просмотр сценария открыт всем
    public async Task<IActionResult> OnGet(string slug, CancellationToken ct)
    {
        Scenario = await _db.Scenarios
            .Include(s => s.Steps)
            .Where(s => s.IsPublished && s.Slug == slug)
            .FirstOrDefaultAsync(ct);

        return Scenario is null ? NotFound() : Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostStart(string slug, CancellationToken ct)
    {
        // 1) получаем GUID пользователя из Identity
        var userIdStr = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userIdStr)) return Challenge(); // не залогинен

        if (!Guid.TryParse(userIdStr, out _))
            return Forbid("User Id must be GUID.");

        // 2) ищем сценарий по slug
        var scenarioId = await _db.Scenarios
            .Where(x => x.IsPublished && x.Slug == slug)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (scenarioId == Guid.Empty) return NotFound();

        // 3) стартуем попытку через сервис (внутри он вызовет Attempt.Start)
        var res = await _attempts.StartAsync(userIdStr, new StartAttemptRequest(scenarioId), ct);

        // 4) редирект на прохождение
        return RedirectToPage("/Attempts/Play", new { id = res.AttemptId});
    }
}
