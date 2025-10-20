using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Identity;

namespace Web.Pages.Admin.Roles;

[Authorize(Roles="Admin")]
public class IndexModel : PageModel
{
    private readonly RoleManager<ApplicationRole> _roleMgr;
    public IList<ApplicationRole> Roles { get; private set; } = new List<ApplicationRole>();

    public IndexModel(RoleManager<ApplicationRole> roleMgr) => _roleMgr = roleMgr;

    public void OnGet() => Roles = _roleMgr.Roles.OrderBy(r => r.Name).ToList();
}
