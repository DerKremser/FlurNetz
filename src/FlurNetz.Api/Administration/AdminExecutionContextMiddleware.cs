using System.Diagnostics;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Api.Administration;

public sealed class AdminExecutionContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IAdminExecutionContextAccessor contextAccessor)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(httpContext.User.FindFirst(AdminAuthenticationDefaults.CommunityIdentityIdClaim)?.Value, out var id)
            && long.TryParse(httpContext.User.FindFirst(AdminAuthenticationDefaults.CredentialVersionClaim)?.Value, out _)
            && httpContext.User.FindFirst(AdminAuthenticationDefaults.LoginNameClaim)?.Value is { } loginName)
        {
            var correlationId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
            contextAccessor.Current = new AdminExecutionContext(
                CommunityIdentityId.Create(id),
                loginName,
                correlationId,
                PermissionCatalog.All);
        }

        try
        {
            await next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            contextAccessor.Current = null;
        }
    }
}
