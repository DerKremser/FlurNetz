using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authentication;

namespace FlurNetz.Api.Endpoints;

public static class AdminAccountEndpoints
{
    public static IEndpointRouteBuilder MapAdminAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/admin/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(AdminAuthenticationDefaults.Scheme).ConfigureAwait(false);
            return Results.Redirect("/admin/login");
        })
        .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.Access))
        .RequireAntiforgery();
        return endpoints;
    }
}
