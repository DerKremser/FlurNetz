using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin.Identities;

[Authorize(Policy = "Admin.Identity.Read")]
public sealed class IndexModel(ICommunityIdentityRead identityRead) : PageModel
{
    public IReadOnlyList<CommunityIdentitySummary> Items { get; private set; } = [];
    public CommunityIdentityId? NextCursor { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(string? after, CancellationToken cancellationToken)
    {
        CommunityIdentityId? cursor = null;
        if (after is not null)
        {
            if (!Guid.TryParse(after, out var raw) || raw == Guid.Empty)
            {
                Error = "Der Seiten-Cursor ist ungültig.";
                return;
            }

            cursor = CommunityIdentityId.Create(raw);
        }

        try
        {
            var page = await identityRead.ListAsync(cursor, 25, cancellationToken).ConfigureAwait(false);
            Items = page.Items;
            NextCursor = page.NextCursor;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = "Die Identitäten sind momentan nicht verfügbar.";
        }
    }
}
