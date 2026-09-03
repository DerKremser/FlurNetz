namespace FlurNetz.Api.Administration;

public sealed class AdminSecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var isAdminWeb = context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/overlay", StringComparison.OrdinalIgnoreCase);
        var isAdminApi = context.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase);
        if (isAdminWeb || isAdminApi)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Content-Security-Policy"] = isAdminWeb
                    ? "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"
                    : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
                return Task.CompletedTask;
            });
        }

        await next(context).ConfigureAwait(false);
    }
}
