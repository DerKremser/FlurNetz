using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;

namespace FlurNetz.Api.Endpoints;

internal static class AntiforgeryEndpointExtensions
{
    public static RouteHandlerBuilder RequireAntiforgery(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new RequiredAntiforgeryMetadata());
    }

    private sealed class RequiredAntiforgeryMetadata : IAntiforgeryMetadata
    {
        public bool RequiresValidation => true;
    }
}
