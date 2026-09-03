using System.ComponentModel.DataAnnotations;
using FlurNetz.Api.Administration;
using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace FlurNetz.Api.Pages.Admin;

[AllowAnonymous]
[EnableRateLimiting("AdminSetup")]
[ValidateAntiForgeryToken]
public sealed class SetupModel(IAdminFirstRunSetup firstRunSetup) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Die E-Mail-Adresse ist erforderlich.")]
    [EmailAddress(ErrorMessage = "Die E-Mail-Adresse ist ungültig.")]
    public string? Email { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Das Passwort ist erforderlich.")]
    public string? NewPassword { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Die Passwortbestätigung ist erforderlich.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Die Passwortbestätigung stimmt nicht überein.")]
    public string? NewPasswordConfirmation { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Der Einrichtungsschlüssel ist erforderlich.")]
    public string? SetupSecret { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyNoStore();
        return await firstRunSetup.IsAvailableAsync(cancellationToken).ConfigureAwait(false)
            ? Page()
            : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ApplyNoStore();
        if (!ModelState.IsValid)
        {
            ClearSensitiveFields();
            return Page();
        }

        try
        {
            var credential = await firstRunSetup.CreateFirstAdministratorAsync(
                    Email,
                    NewPassword,
                    NewPasswordConfirmation,
                    SetupSecret,
                    cancellationToken)
                .ConfigureAwait(false);
            await HttpContext.SignInAsync(
                AdminAuthenticationDefaults.Scheme,
                AdminPrincipalFactory.Create(credential),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                }).ConfigureAwait(false);
            return LocalRedirect("/admin");
        }
        catch (AdminSetupClosedException)
        {
            ClearSensitiveFields();
            return NotFound();
        }
        catch (AdminSetupGateException)
        {
            AddGenericSetupError();
            ClearSensitiveFields();
            return Page();
        }
        catch (ArgumentException)
        {
            ClearSensitiveFields();
            AddGenericSetupError();
            return Page();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ClearSensitiveFields();
            AddGenericSetupError();
            return Page();
        }
    }

    private void ApplyNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private void ClearSensitiveFields()
    {
        NewPassword = null;
        NewPasswordConfirmation = null;
        SetupSecret = null;
        ModelState.Remove(nameof(NewPassword));
        ModelState.Remove(nameof(NewPasswordConfirmation));
        ModelState.Remove(nameof(SetupSecret));
    }

    private void AddGenericSetupError() => ModelState.AddModelError(
        string.Empty,
        "Die Ersteinrichtung konnte nicht abgeschlossen werden.");
}
