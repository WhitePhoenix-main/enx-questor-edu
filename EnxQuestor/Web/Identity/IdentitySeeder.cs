using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        // Ensure roles
        foreach (var role in new[] { "Admin", "Teacher", "Student" })
        {
            if (await roleMgr.FindByNameAsync(role) is null)
                await roleMgr.CreateAsync(new ApplicationRole { Name = role });
        }

        // Dev admin
        var adminEmail = "admin@example.com";
        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var res = await userMgr.CreateAsync(admin, "Admin123$");
            if (!res.Succeeded) throw new Exception("Failed to create dev admin: " + string.Join("; ", res.Errors.Select(e => e.Description)));
        }
        if (!await userMgr.IsInRoleAsync(admin, "Admin"))
            await userMgr.AddToRoleAsync(admin, "Admin");

        // Default CRUD claims for Teacher
        var teacherRole = await roleMgr.FindByNameAsync("Teacher");
        if (teacherRole is not null)
        {
            // nothing special here; example shows claims on users typically
        }
    }
}
