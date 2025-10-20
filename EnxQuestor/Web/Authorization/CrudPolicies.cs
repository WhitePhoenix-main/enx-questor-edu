using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Authorization;

public static class CrudPolicies
{
    public const string Create = "crud:create";
    public const string Read   = "crud:read";
    public const string Update = "crud:update";
    public const string Delete = "crud:delete";

    public static IServiceCollection AddCrudPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Create, p => p.RequireAssertion(ctx => Has(ctx, Create)));
            options.AddPolicy(Read,   p => p.RequireAssertion(ctx => Has(ctx, Read)));
            options.AddPolicy(Update, p => p.RequireAssertion(ctx => Has(ctx, Update)));
            options.AddPolicy(Delete, p => p.RequireAssertion(ctx => Has(ctx, Delete)));
        });
        return services;
    }

    private static bool Has(AuthorizationHandlerContext ctx, string perm)
    {
        // Grant for Admin role
        if (ctx.User.IsInRole("Admin")) return true;

        // Check claims like "perm: crud:create"
        return ctx.User.HasClaim("perm", perm) ||
               ctx.User.Claims.Any(c => c.Type == "perm" && c.Value == perm);
    }
}
