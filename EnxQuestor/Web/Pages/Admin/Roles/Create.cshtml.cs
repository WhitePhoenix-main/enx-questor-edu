using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Identity;

namespace Web.Pages.Admin.Roles;

[Authorize(Roles="Admin")]
public class CreateModel : PageModel
{
    private readonly RoleManager<ApplicationRole> _roleMgr;
    public CreateModel(RoleManager<ApplicationRole> roleMgr) => _roleMgr = roleMgr;

    [BindProperty, Required, MinLength(2)]
    public string Name { get; set; } = default!;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var res = await _roleMgr.CreateAsync(new ApplicationRole { Name = Name });
        if (res.Succeeded) return RedirectToPage("Index");
        foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
        return Page();
    }
}
