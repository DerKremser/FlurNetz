using System.ComponentModel.DataAnnotations;
using FlurNetz.Api.Administration;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Pages.Admin;

[AllowAnonymous]
[EnableRateLimiting("AdminSetup")]
[ValidateAntiForgeryToken]
public sealed class SetupModel(
    IAdminFirstRunSetup firstRunSetup,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_EmailInvalid))]
    public string? Email { get; set; }

    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_PasswordRequired))]
    public string? NewPassword { get; set; }

    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_PasswordConfirmationRequired))]
    [Compare(nameof(NewPassword), ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_PasswordConfirmationMismatch))]
    public string? NewPasswordConfirmation { get; set; }

    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_SetupSecretRequired))]
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
            AdminCultureCookie.Append(HttpContext, AdminPreferredCulture.Default);
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
        localizer["Error_SetupFailed"].Value);
}
