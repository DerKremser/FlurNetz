using System.ComponentModel.DataAnnotations;
using FlurNetz.Api.Administration;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using FlurNetz.Modules.Administration.Domain;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Administration.Access")]
public sealed class AccountModel(
    AdminPasswordChange passwordChange,
    AdminPreferredCultureChange preferredCultureChange,
    IAdminCredentialStore credentialStore,
    IAdminExecutionContextAccessor contextAccessor,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_CurrentPasswordRequired))]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_NewPasswordRequired))]
    public string? NewPassword { get; set; }

    [BindProperty]
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_PasswordConfirmationRequired))]
    [Compare(nameof(NewPassword), ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = nameof(SharedResource.Validation_PasswordConfirmationMismatch))]
    public string? NewPasswordConfirmation { get; set; }

    [BindProperty]
    public string PreferredCulture { get; set; } = AdminPreferredCulture.Default;

    public bool Success { get; private set; }

    [TempData]
    public bool LanguageSuccess { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await LoadPreferredCultureAsync(cancellationToken).ConfigureAwait(false);
        return result ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPreferredCultureAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var context = contextAccessor.Current;
        if (context is null)
        {
            return Challenge(AdminAuthenticationDefaults.Scheme);
        }

        try
        {
            var credential = await passwordChange.ChangeAsync(context, CurrentPassword!, NewPassword!, cancellationToken).ConfigureAwait(false);
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
            Success = true;
            CurrentPassword = null;
            NewPassword = null;
            NewPasswordConfirmation = null;
            await LoadPreferredCultureAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }
        catch (InvalidCredentialException)
        {
            ModelState.AddModelError(string.Empty, localizer["Error_CurrentPasswordInvalid"].Value);
            await LoadPreferredCultureAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }
        catch (ArgumentException exception)
        {
            var message = exception.ParamName == "password"
                ? localizer["Validation_PasswordLength", AdminPasswordPolicy.MinimumLength, AdminPasswordPolicy.MaximumLength].Value
                : localizer["Error_PasswordChangeFailed"].Value;
            ModelState.AddModelError(nameof(NewPassword), message);
            await LoadPreferredCultureAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLanguageAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        var context = contextAccessor.Current;
        if (context is null)
        {
            return Challenge(AdminAuthenticationDefaults.Scheme);
        }

        if (!AdminPreferredCulture.TryNormalize(PreferredCulture, out var normalizedCulture))
        {
            var result = await LoadPreferredCultureAsync(cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                return result;
            }

            ModelState.AddModelError(
                nameof(PreferredCulture),
                localizer["Account_LanguageInvalid"].Value);
            return Page();
        }

        await preferredCultureChange
            .ChangeAsync(context, normalizedCulture, cancellationToken)
            .ConfigureAwait(false);
        AdminCultureCookie.Append(HttpContext, normalizedCulture);
        LanguageSuccess = true;
        return RedirectToPage();
    }

    private async Task<IActionResult?> LoadPreferredCultureAsync(CancellationToken cancellationToken)
    {
        var context = contextAccessor.Current;
        if (context is null)
        {
            return Challenge(AdminAuthenticationDefaults.Scheme);
        }

        var credential = await credentialStore
            .GetByIdentityAsync(context.ActorCommunityIdentityId, cancellationToken)
            .ConfigureAwait(false);
        if (credential is null)
        {
            return Challenge(AdminAuthenticationDefaults.Scheme);
        }

        PreferredCulture = AdminPreferredCulture.Resolve(credential.PreferredCulture);
        return null;
    }
}
