using FlurNetz.Modules.Administration.Domain;
using Microsoft.AspNetCore.Localization;

namespace FlurNetz.Api.Administration;

public static class AdminCultureCookie
{
    public static void Append(HttpContext context, string? preferredCulture)
    {
        ArgumentNullException.ThrowIfNull(context);
        var culture = AdminPreferredCulture.Require(preferredCulture);

        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                Secure = context.Request.IsHttps
                    || context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction(),
                Path = "/"
            });
    }
}
