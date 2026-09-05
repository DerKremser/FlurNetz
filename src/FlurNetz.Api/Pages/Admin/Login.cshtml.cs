using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FlurNetz.Api.Administration;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Pages.Admin;

[AllowAnonymous]
[EnableRateLimiting("AdminLogin")]
public sealed class LoginModel(
    IAdminAuthenticationService authenticationService,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_EmailInvalid))]
    public string? Email { get; set; }

    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_PasswordRequired))]
    public string? Password { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
        if (!IsLocalReturnUrl(ReturnUrl))
        {
            ReturnUrl = "/admin";
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IsLocalReturnUrl(ReturnUrl))
        {
            ReturnUrl = "/admin";
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authenticationService.AuthenticateAsync(Email, Password, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Credential is null)
        {
            ModelState.AddModelError(string.Empty, localizer["Error_InvalidCredentials"].Value);
            return Page();
        }

        await HttpContext.SignInAsync(
            AdminAuthenticationDefaults.Scheme,
            AdminPrincipalFactory.Create(result.Credential),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            }).ConfigureAwait(false);
        AdminCultureCookie.Append(HttpContext, AdminPreferredCulture.Resolve(result.Credential.PreferredCulture));
        return LocalRedirect(ReturnUrl!);
    }

    private static bool IsLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith("/", StringComparison.Ordinal)
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}
