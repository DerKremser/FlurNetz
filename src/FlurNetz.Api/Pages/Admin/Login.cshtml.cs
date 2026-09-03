using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FlurNetz.Api.Administration;
using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin;

[AllowAnonymous]
[EnableRateLimiting("AdminLogin")]
public sealed class LoginModel(IAdminAuthenticationService authenticationService) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Die E-Mail-Adresse ist erforderlich.")]
    [EmailAddress(ErrorMessage = "Die E-Mail-Adresse ist ungültig.")]
    public string? Email { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Das Passwort ist erforderlich.")]
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
            ModelState.AddModelError(string.Empty, "Anmeldedaten sind ungültig.");
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
        return LocalRedirect(ReturnUrl!);
    }

    private static bool IsLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith("/", StringComparison.Ordinal)
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}
