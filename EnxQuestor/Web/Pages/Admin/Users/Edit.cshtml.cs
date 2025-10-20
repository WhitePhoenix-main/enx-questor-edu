using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Identity;

namespace Web.Pages.Admin.Users;

[Authorize(Roles="Admin")]
public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly RoleManager<ApplicationRole> _roleMgr;

    public string UserId { get; private set; } = default!;
    public string UserEmail { get; private set; } = default!;
    [BindProperty] public List<string> SelectedRoles { get; set; } = new();
    public List<string> AllRoles { get; private set; } = new();

    public EditModel(UserManager<ApplicationUser> userMgr, RoleManager<ApplicationRole> roleMgr)
    {
        _userMgr = userMgr;
        _roleMgr = roleMgr;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var u = await _userMgr.FindByIdAsync(id);
        if (u is null) return NotFound();
        UserId = u.Id;
        UserEmail = u.Email ?? u.UserName ?? "(no email)";
        AllRoles = _roleMgr.Roles.Select(r => r.Name!).OrderBy(x => x).ToList();
        SelectedRoles = (await _userMgr.GetRolesAsync(u)).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var u = await _userMgr.FindByIdAsync(id);
        if (u is null) return NotFound();

        var all = _roleMgr.Roles.Select(r => r.Name!).ToList();
        var current = await _userMgr.GetRolesAsync(u);

        var toAdd = SelectedRoles.Except(current).Intersect(all).ToList();
        var toRemove = current.Except(SelectedRoles).ToList();

        if (toAdd.Any()) await _userMgr.AddToRolesAsync(u, toAdd);
        if (toRemove.Any()) await _userMgr.RemoveFromRolesAsync(u, toRemove);

        return RedirectToPage("Index");
    }
}
