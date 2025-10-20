using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Identity;

namespace Web.Pages.Admin.Roles;

[Authorize(Roles="Admin")]
public class DeleteModel : PageModel
{
    private readonly RoleManager<ApplicationRole> _roleMgr;
    public ApplicationRole? Role { get; private set; }

    public DeleteModel(RoleManager<ApplicationRole> roleMgr) => _roleMgr = roleMgr;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        Role = await _roleMgr.FindByIdAsync(id);
        return Role is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var r = await _roleMgr.FindByIdAsync(id);
        if (r is null) return NotFound();
        await _roleMgr.DeleteAsync(r);
        return RedirectToPage("Index");
    }
}
