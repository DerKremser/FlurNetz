using System.ComponentModel.DataAnnotations;
using FlurNetz.Api.Administration;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Administration.Access")]
public sealed class AccountModel(
    AdminPasswordChange passwordChange,
    IAdminExecutionContextAccessor contextAccessor) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Das aktuelle Passwort ist erforderlich.")]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Das neue Passwort ist erforderlich.")]
    public string? NewPassword { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Die Passwortbestätigung ist erforderlich.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Die Passwortbestätigung stimmt nicht überein.")]
    public string? NewPasswordConfirmation { get; set; }

    public bool Success { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
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
            return Page();
        }
        catch (InvalidCredentialException)
        {
            ModelState.AddModelError(string.Empty, "Das aktuelle Passwort ist ungültig.");
            return Page();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(NewPassword), exception.Message);
            return Page();
        }
    }
}
