using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Infrastructure.Persistence;
using Web.Identity;

namespace Web.Pages.Me;

[Authorize]
public sealed class ProfileModel : PageModel
{
    private readonly AppDbContext _db;                      // доменная БД
    private readonly UserManager<ApplicationUser> _um;      // ASP.NET Identity

    public string? TelegramId { get; private set; }
    public string? OneTimeCode { get; private set; }

    public ProfileModel(AppDbContext db, UserManager<ApplicationUser> um)
    {
        _db = db;
        _um = um;
    }

    public async Task OnGetAsync()
    {
        var user = await _um.GetUserAsync(User);
        TelegramId = user?.TelegramId;
    }

    public async Task OnPostGenerateAsync()
    {
        var user = await _um.GetUserAsync(User);
        if (user is null) return;

        var code = Guid.NewGuid().ToString("N")[..8];
        var link = Domain.Telegram.TelegramLink.Create(user.Id, code, DateTimeOffset.UtcNow.AddMinutes(15));
        _db.TelegramLinks.Add(link);
        await _db.SaveChangesAsync();

        OneTimeCode = code;
    }
}