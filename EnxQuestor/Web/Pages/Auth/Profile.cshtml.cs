using System;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize]
public sealed class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    public string? TelegramId { get; private set; }
    public string? OneTimeCode { get; private set; }
    public ProfileModel(AppDbContext db) => _db = db;

    public async Task OnGet()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var u = await _db.Users.FindAsync(uid);
        TelegramId = u?.TelegramId;
    }

    public async Task OnPostGenerate()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var code = Guid.NewGuid().ToString("N")[..8];
        var link = Domain.Telegram.TelegramLink.Create(uid, code, DateTimeOffset.UtcNow.AddMinutes(15));
        _db.TelegramLinks.Add(link);
        await _db.SaveChangesAsync();
        OneTimeCode = code;
    }
}