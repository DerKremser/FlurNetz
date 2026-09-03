using Microsoft.AspNetCore.Antiforgery;

namespace FlurNetz.Api.Administration;

/// <summary>
/// Sichert cookie-authentifizierte Minimal-API-Mutationen explizit gegen CSRF.
/// Die eingebaute Antiforgery-Middleware bleibt zusätzlich aktiv; diese schmale
/// Host-Grenze stellt sicher, dass alle Admin-API-Mutationen denselben Nachweis verlangen.
/// </summary>
public sealed class AdminAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var isAdminMutation = (context.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/admin/logout", StringComparison.OrdinalIgnoreCase))
            && !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method)
            && !HttpMethods.IsTrace(context.Request.Method);

        if (isAdminMutation)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException exception)
            {
                context.RequestServices
                    .GetRequiredService<ILogger<AdminAntiforgeryMiddleware>>()
                    .LogWarning("Admin-CSRF-Nachweis für {Method} {Path} wurde abgelehnt: {Reason}.", context.Request.Method, context.Request.Path, exception.Message);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
