using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Administration.Dashboard.Read")]
public sealed class IndexModel(IAdminAuditStore auditStore, ICommunityIdentityRead identityRead) : PageModel
{
    public IReadOnlyList<AdminAuditEntry> RecentAudit { get; private set; } = [];
    public int IdentityCount { get; private set; }
    public int AuditCount => RecentAudit.Count;
    public string? DependencyError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var page = await identityRead.ListAsync(null, 5, cancellationToken).ConfigureAwait(false);
            IdentityCount = page.Items.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DependencyError = "Identitäten";
        }

        try
        {
            RecentAudit = await auditStore.ListAsync(8, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DependencyError = DependencyError is null ? "Audit" : $"{DependencyError} und Audit";
        }
    }
}
