using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin;

[Authorize(Policy = "Admin.Shop.Read")]
public sealed class ShopModel(ListShopOffers reader) : PageModel
{
    public IReadOnlyList<ShopOffer> Items { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken token)
    {
        try { Items = await reader.ExecuteAsync(token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { Error = "Der Shop-Katalog ist momentan nicht verfügbar."; }
    }
}
