using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Integrations.Read")]
public sealed class IntegrationsModel(IExternalIdentityMappingStore reader) : PageModel
{
    public IReadOnlyList<ExternalIdentityMapping> Items { get; private set; } = [];
    public string? Error { get; private set; }
    public async Task OnGetAsync(CancellationToken token)
    {
        try { Items = await reader.ListAsync(token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { Error = "Die Integrations-Mappings sind momentan nicht verfügbar."; }
    }
}
