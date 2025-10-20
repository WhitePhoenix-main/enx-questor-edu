using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Identity;

namespace Web.Pages.Admin.Users;

[Authorize(Roles="Admin")]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly RoleManager<ApplicationRole> _roleMgr;
    public List<UserRow> Users { get; private set; } = new();

    public sealed record UserRow(string Id, string Email, IEnumerable<string> Roles);

    public IndexModel(UserManager<ApplicationUser> userMgr, RoleManager<ApplicationRole> roleMgr)
    {
        _userMgr = userMgr;
        _roleMgr = roleMgr;
    }

    public async Task OnGetAsync()
    {
        var all = _userMgr.Users.ToList();
        Users = new();
        foreach (var u in all)
        {
            var roles = await _userMgr.GetRolesAsync(u);
            Users.Add(new UserRow(u.Id, u.Email ?? u.UserName ?? "(no email)", roles));
        }
    }
}
