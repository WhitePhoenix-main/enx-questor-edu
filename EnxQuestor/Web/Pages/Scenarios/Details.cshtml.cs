using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Web.Pages.Scenarios;

[AllowAnonymous]
public sealed class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    public Domain.Scenarios.Scenario? Scenario { get; private set; }

    public DetailsModel(AppDbContext db) => _db = db;

    public async Task<IActionResult> OnGet(string slug)
    {
        Scenario = await _db.Scenarios
            .Include(s => s.Steps)
            .Where(s => s.IsPublished && s.Slug == slug)
            .FirstOrDefaultAsync();

        if (Scenario is null) return NotFound();
        return Page();
    }

    // If IAttemptService is available at runtime, we can start entirely within Razor Pages.
    public async Task<IActionResult> OnPostStart(string slug, [FromServices] Application.Abstractions.IAttemptService svc)
    {
        var s = await _db.Scenarios.Where(x => x.IsPublished && x.Slug == slug)
            .Select(x => new { x.Id }).FirstOrDefaultAsync();

        if (s is null) return NotFound();

        // We don't know exact DTO names at compile-time here;
        // below we try two common shapes via dynamic dispatch to be resilient.
        try
        {
            // Try: StartAsync(string userId, StartAttemptRequest req, CancellationToken ct)
            var uid = User?.Identity?.Name ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (uid is null) return Challenge(); // require auth

            var reqType = typeof(Application.DTO.StartAttemptRequest);
            var req = System.Activator.CreateInstance(reqType, new object?[] { s.Id });
            var method = svc.GetType().GetMethod("StartAsync");
            if (method is null) throw new System.MissingMethodException("StartAsync not found on IAttemptService");

            var task = (System.Threading.Tasks.Task) method.Invoke(svc, new object?[] { uid, req!, HttpContext.RequestAborted })!;
            await task.ConfigureAwait(false);

            // Try to get result.Id via reflection if method returns a result with Id
            var resultProp = task.GetType().GetProperty("Result");
            var result = resultProp?.GetValue(task);
            var idProp = result?.GetType().GetProperty("Id") ?? result?.GetType().GetProperty("AttemptId");
            var attemptId = (System.Guid?) idProp?.GetValue(result);

            if (attemptId is System.Guid gid)
                return RedirectToPage("/Attempts/Play", new { id = gid });

            // If result shape unknown, fall back to client-side API call
        }
        catch
        {
            // Fall through to client-side API start
        }

        // Fallback: render page and let the client-side script call the REST API.
        TempData["Toast"] = "Не удалось запустить попытку через серверный обработчик — пробуем через API.";
        return Page();
    }
}
