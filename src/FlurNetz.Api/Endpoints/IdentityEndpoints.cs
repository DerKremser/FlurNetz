using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Identity.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace FlurNetz.Api.Endpoints;

/// <summary>
/// Ordnet die HTTP-Grenze des aktuell eingebundenen Identity-Vertical-Slices zu.
/// </summary>
public static class IdentityEndpoints
{
    /// <summary>
    /// Registriert den Endpunkt zum Erzeugen einer internen Community-Identität.
    /// </summary>
    /// <param name="endpoints">Die Route-Builder des API-Hosts.</param>
    /// <returns>Der übergebene Route-Builder für weitere Zuordnungen.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/identities", CreateCommunityIdentityAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateCommunityIdentityAsync(
        CreateCommunityIdentity useCase,
        CancellationToken cancellationToken)
    {
        // Fachliche Erzeugung und Persistierung bleiben vollständig im Identity-Use-Case.
        var identityId = await useCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        // Das API-DTO trennt den HTTP-Vertrag bewusst von Domain- und Contract-Typen.
        return Results.Created(
            uri: (string?)null,
            value: new CreateCommunityIdentityResponse(identityId.Value));
    }
}
