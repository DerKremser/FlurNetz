using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Contracts;
using FlurNetz.Modules.Integrations.Domain;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Domain;
using Microsoft.AspNetCore.Mvc;

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

        endpoints.MapPost(MappingsRoute, LinkAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.IntegrationsManageMappings))
            .RequireAntiforgery();
        endpoints.MapGet($"{MappingsRoute}/{{provider}}/{{externalUserId}}", GetAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.IntegrationsRead));
        endpoints.MapGet($"{MappingsRoute}/community/{{communityIdentityId}}", ListAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.IntegrationsRead));
        endpoints.MapDelete($"{MappingsRoute}/{{provider}}/{{externalUserId}}", UnlinkAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.IntegrationsManageMappings))
            .RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> LinkAsync(
        [FromBody] ExternalIdentityMappingRequest? request,
        IExternalIdentityMappingStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
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
            if (!TryHighRiskRequest(request.Reason, request.RequestId, out var requestData, out var requestError))
            {
                return InvalidRequest(requestError!);
            }

            var provider = IntegrationProviderKey.Create(request.Provider!);
            var externalUserId = ExternalUserId.Create(request.ExternalUserId!);
            var targetIdentity = CommunityIdentityId.Create(communityIdentityId);
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var mapping = ExternalIdentityMapping.Create(provider, externalUserId, targetIdentity);
            var linkResult = ExternalIdentityLinkStatus.Linked;
            await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestData.RequestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.ExternalIdentityLinked,
                        "ExternalIdentityMapping",
                        $"{provider.Value}/{externalUserId.Value}",
                        AdminRequestFingerprint.Compute(("provider", provider.Value), ("externalUserId", externalUserId.Value), ("communityIdentityId", targetIdentity.Value), ("reason", requestData.Reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, token) =>
                    {
                        var result = await store.LinkAsync(mapping, connection, transaction, token).ConfigureAwait(false);
                        linkResult = result.Status;
                        if (result.Status == ExternalIdentityLinkStatus.CommunityIdentityNotFound) throw new CommunityIdentityNotFoundForExternalMappingException(targetIdentity);
                        if (result.Status == ExternalIdentityLinkStatus.Conflict) throw new ExternalIdentityMappingConflictException(provider, externalUserId, result.ExistingCommunityIdentityId!.Value, targetIdentity);
                    },
                    () => CreateAudit(context, AdminAuditActions.ExternalIdentityLinked, $"{provider.Value}/{externalUserId.Value}", requestData.Reason, requestData.RequestId, new Dictionary<string, string?> { ["Linked"] = "true" }),
                    cancellationToken).ConfigureAwait(false);

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
        catch (AdminOperationConflictException exception)
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
        [FromBody] AdminActionRequest? request,
        IExternalIdentityMappingStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryHighRiskRequest(request?.Reason, request?.RequestId, out var requestData, out var requestError))
        {
            return InvalidRequest(requestError!);
        }

        try
        {
            var validProvider = IntegrationProviderKey.Create(provider);
            var validExternalUserId = ExternalUserId.Create(externalUserId);
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestData.RequestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.ExternalIdentityUnlinked,
                        "ExternalIdentityMapping",
                        $"{validProvider.Value}/{validExternalUserId.Value}",
                        AdminRequestFingerprint.Compute(("provider", validProvider.Value), ("externalUserId", validExternalUserId.Value), ("reason", requestData.Reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, token) =>
                    {
                        if (!await store.UnlinkAsync(validProvider, validExternalUserId, connection, transaction, token).ConfigureAwait(false))
                        {
                            throw new KeyNotFoundException();
                        }
                    },
                    () => CreateAudit(context, AdminAuditActions.ExternalIdentityUnlinked, $"{validProvider.Value}/{validExternalUserId.Value}", requestData.Reason, requestData.RequestId, new Dictionary<string, string?> { ["Unlinked"] = "true" }),
                    cancellationToken).ConfigureAwait(false);

            return Results.NoContent();
        }
        catch (AdminOperationConflictException exception) { return Conflict(exception.Message); }
        catch (KeyNotFoundException) { return NotFoundMapping(IntegrationProviderKey.Create(provider), ExternalUserId.Create(externalUserId)); }
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

    private static bool TryHighRiskRequest(
        string? rawReason,
        Guid? rawRequestId,
        out (Guid RequestId, string Reason) value,
        out string? error)
    {
        value = default;
        try
        {
            if (rawRequestId is not Guid requestId || requestId == Guid.Empty)
            {
                throw new ArgumentException("Eine eindeutige RequestId ist erforderlich.");
            }

            value = (requestId, AdminReason.Required(rawReason));
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static AdminAuditEntry CreateAudit(
        AdminExecutionContext context,
        string action,
        string targetId,
        string reason,
        Guid requestId,
        IReadOnlyDictionary<string, string?> changeSummary) =>
        new(
            Guid.NewGuid(),
            context.ActorCommunityIdentityId,
            context.ActorLoginName,
            action,
            "ExternalIdentityMapping",
            targetId,
            null,
            AdminRiskLevel.High,
            reason,
            AdminAuditOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            context.CorrelationId,
            requestId,
            null,
            changeSummary,
            new Dictionary<string, string?>());
}
