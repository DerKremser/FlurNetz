using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Overlay.Read")]
public sealed class OverlayModel(
    ListOverlayChannels reader,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<OverlayChannel> Items { get; private set; } = [];
    public string? Error { get; private set; }
    public async Task OnGetAsync(CancellationToken token)
    {
        try { Items = await reader.ExecuteAsync(token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { Error = localizer["Error_OverlayUnavailable"].Value; }
    }
}
