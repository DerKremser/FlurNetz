using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;

namespace FlurNetz.Api.Endpoints;

/// <summary>
/// Ordnet die interne Management-Grenze für externe Identitäts-Mappings zu.
/// </summary>
/// <remarks>
/// Diese API ist bis zu einem späteren Administration-/Security-Slice nicht für eine
/// ungeschützte externe Produktivexposition vorgesehen.
/// </remarks>
public static class IntegrationsManagementEndpoints
{
    private const string MappingsRoute = "/api/admin/integrations/external-identities";

    /// <summary>Registriert Link, Get/List und Unlink der V1-Mappings.</summary>
    public static IEndpointRouteBuilder MapIntegrationsManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(MappingsRoute, LinkAsync);
        endpoints.MapGet($"{MappingsRoute}/{{provider}}/{{externalUserId}}", GetAsync);
        endpoints.MapGet($"{MappingsRoute}/community/{{communityIdentityId}}", ListAsync);
        endpoints.MapDelete($"{MappingsRoute}/{{provider}}/{{externalUserId}}", UnlinkAsync);

        return endpoints;
    }

    private static async Task<IResult> LinkAsync(
        ExternalIdentityMappingRequest? request,
        LinkExternalIdentity useCase,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidRequest("Der Request-Body ist erforderlich.");
        }

        if (request.CommunityIdentityId is not Guid communityIdentityId
            || communityIdentityId == Guid.Empty)
        {
            return InvalidRequest("Die Community-Identity-ID ist erforderlich und muss eine nicht leere GUID sein.");
        }

        try
        {
            var mapping = await useCase.ExecuteAsync(
                    IntegrationProviderKey.Create(request.Provider!),
                    ExternalUserId.Create(request.ExternalUserId!),
                    CommunityIdentityId.Create(communityIdentityId),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Created(
                MappingRoute(mapping.ProviderKey, mapping.ExternalUserId),
                ToResponse(mapping));
        }
        catch (CommunityIdentityNotFoundForExternalMappingException exception)
        {
            return NotFoundCommunityIdentity(exception.CommunityIdentityId);
        }
        catch (ExternalIdentityMappingConflictException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> GetAsync(
        string provider,
        string externalUserId,
        GetExternalIdentityMapping useCase,
        CancellationToken cancellationToken)
    {
        try
        {
            var validProvider = IntegrationProviderKey.Create(provider);
            var validExternalUserId = ExternalUserId.Create(externalUserId);
            var mapping = await useCase.ExecuteAsync(
                    validProvider,
                    validExternalUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            return mapping is null
                ? NotFoundMapping(validProvider, validExternalUserId)
                : Results.Ok(ToResponse(mapping));
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> ListAsync(
        string communityIdentityId,
        ListExternalIdentityMappings useCase,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(communityIdentityId, out var rawId) || rawId == Guid.Empty)
        {
            return InvalidRequest("Die Community-Identity-ID der Route ist ungültig.");
        }

        try
        {
            var mappings = await useCase.ExecuteAsync(
                    CommunityIdentityId.Create(rawId),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new ExternalIdentityMappingListResponse(
                mappings.Select(ToResponse).ToArray()));
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static async Task<IResult> UnlinkAsync(
        string provider,
        string externalUserId,
        UnlinkExternalIdentity useCase,
        CancellationToken cancellationToken)
    {
        try
        {
            var validProvider = IntegrationProviderKey.Create(provider);
            var validExternalUserId = ExternalUserId.Create(externalUserId);
            var removed = await useCase.ExecuteAsync(
                    validProvider,
                    validExternalUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            return removed
                ? Results.NoContent()
                : NotFoundMapping(validProvider, validExternalUserId);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    private static ExternalIdentityMappingResponse ToResponse(
        ExternalIdentityMapping mapping) =>
        new(
            mapping.ProviderKey.Value,
            mapping.ExternalUserId.Value,
            mapping.CommunityIdentityId.Value);

    private static string MappingRoute(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId) =>
        $"{MappingsRoute}/{Uri.EscapeDataString(providerKey.Value)}/{Uri.EscapeDataString(externalUserId.Value)}";

    private static IResult InvalidRequest(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Ungültige Anfrage.",
            detail: detail);

    private static IResult NotFoundCommunityIdentity(CommunityIdentityId id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Community-Identität nicht gefunden.",
            detail: $"Die Community-Identität '{id.Value}' wurde nicht gefunden.");

    private static IResult NotFoundMapping(
        IntegrationProviderKey providerKey,
        ExternalUserId externalUserId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "External-Identity-Mapping nicht gefunden.",
            detail: $"Das Mapping '{providerKey}/{externalUserId}' wurde nicht gefunden.");

    private static IResult Conflict(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "External-Identity-Mapping-Konflikt.",
            detail: detail);
}
