using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Audit.Read")]
public sealed class AuditModel(
    IAdminAuditStore auditStore,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<AdminAuditEntry> Entries { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try { Entries = await auditStore.ListAsync(100, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { Error = localizer["Error_AuditUnavailable"].Value; }
    }
}
