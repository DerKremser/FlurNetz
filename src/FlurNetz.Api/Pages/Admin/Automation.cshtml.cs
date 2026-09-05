using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Automation.Application;
using FlurNetz.Modules.Automation.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Automation.Read")]
public sealed class AutomationModel(
    ListAutomationRules reader,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<AutomationRule> Items { get; private set; } = [];
    public string? Error { get; private set; }
    public async Task OnGetAsync(CancellationToken token)
    {
        try { Items = await reader.ExecuteAsync(token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { Error = localizer["Error_AutomationUnavailable"].Value; }
    }
}
