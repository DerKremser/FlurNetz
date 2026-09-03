using System.Diagnostics;
using System.Text.Json;
using FlurNetz.Api.Contracts;
using FlurNetz.Api.Cursors;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Domain;
using Microsoft.AspNetCore.Mvc;

namespace FlurNetz.Api.Endpoints;

/// <summary>Ordnet Overlay-Management, Browser Source und SSE der API-Grenze zu.</summary>
public static class OverlayEndpoints
{
    private const string ChannelsRoute = "/api/admin/overlay/channels";

    /// <summary>Registriert die interne Management-Grenze und den OBS-Transport.</summary>
    public static IEndpointRouteBuilder MapOverlayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(ChannelsRoute, ListAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayRead));
        endpoints.MapGet($"{ChannelsRoute}/{{channelId}}", GetAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayRead));
        endpoints.MapPost(ChannelsRoute, CreateAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayManage))
            .RequireAntiforgery();
        endpoints.MapPut($"{ChannelsRoute}/{{channelId}}", UpdateAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/enable", EnableAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/disable", DisableAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/archive", ArchiveAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayManage))
            .RequireAntiforgery();
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/rotate-source-key", RotateAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayRotateSourceKey))
            .RequireAntiforgery();
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/alerts", PreviewAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.OverlayManage))
            .RequireAntiforgery();
        endpoints.MapGet("/overlay/{sourceKey}", BrowserSourceAsync);
        endpoints.MapGet("/api/overlay/sources/{sourceKey}/stream", StreamAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ListOverlayChannels useCase, CancellationToken cancellationToken)
    {
        var channels = await useCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(new OverlayChannelListResponse(channels.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> GetAsync(string channelId, GetOverlayChannel useCase, CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        var channel = await useCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
        return channel is null ? NotFound(id) : Results.Ok(ToResponse(channel));
    }

    private static async Task<IResult> CreateAsync([FromBody] OverlayChannelRequest? request, IOverlayChannelStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken)
    {
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var channel = OverlayChannel.Create(OverlayChannelId.New(), request.DisplayName!, request.Description, Canonicalize(clock.UtcNow));
            var sourceKey = OverlaySourceKey.Generate();
            await coordinator.ExecuteAuditedAsync(
                (connection, transaction, token) => store.AddAsync(channel, OverlaySourceKey.Hash(sourceKey), connection, transaction, token),
                () => NormalAudit(context, AdminAuditActions.ChannelCreated, channel.Id.Value.ToString("D"), new Dictionary<string, string?> { ["Created"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return Results.Created($"{ChannelsRoute}/{channel.Id.Value:D}", new OverlayChannelSecretResponse(ToResponse(channel), sourceKey, BrowserSourceUrl(sourceKey)));
        }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> UpdateAsync(string channelId, [FromBody] OverlayChannelRequest? request, IOverlayChannelStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            OverlayChannel? channel = null;
            await coordinator.ExecuteAuditedAsync(
                async (connection, transaction, token) => channel = await store.MutateAsync(id, value => value.UpdateMetadata(request.DisplayName!, request.Description, Canonicalize(clock.UtcNow)), connection, transaction, cancellationToken: token).ConfigureAwait(false),
                () => NormalAudit(context, AdminAuditActions.ChannelUpdated, id.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return channel is null ? NotFound(id) : Results.Ok(ToResponse(channel));
        }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static Task<IResult> EnableAsync(string channelId, IOverlayChannelStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken) => StatusAsync(channelId, value => value.Enable(Canonicalize(clock.UtcNow)), AdminAuditActions.ChannelEnabled, store, coordinator, contextAccessor, cancellationToken);
    private static Task<IResult> DisableAsync(string channelId, IOverlayChannelStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, IClock clock, CancellationToken cancellationToken) => StatusAsync(channelId, value => value.Disable(Canonicalize(clock.UtcNow)), AdminAuditActions.ChannelDisabled, store, coordinator, contextAccessor, cancellationToken);
    private static async Task<IResult> ArchiveAsync(
        string channelId,
        [FromBody] AdminActionRequest? request,
        IOverlayChannelStore store,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        if (!TryHighRiskRequest(request, out var requestData, out var requestError)) return InvalidRequest(requestError!);
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestData.RequestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.ChannelArchived,
                        "OverlayChannel",
                        id.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(("channel", id.Value), ("reason", requestData.Reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, token) =>
                    {
                        var channel = await store.MutateAsync(id, value => value.Archive(Canonicalize(clock.UtcNow)), connection, transaction, invalidateSourceKey: true, cancellationToken: token).ConfigureAwait(false);
                        if (channel is null) throw new KeyNotFoundException();
                    },
                    () => CreateAudit(context, AdminAuditActions.ChannelArchived, id.Value.ToString("D"), requestData.Reason, requestData.RequestId, new Dictionary<string, string?> { ["Archived"] = "true" }),
                    cancellationToken).ConfigureAwait(false);
            return mutation.AlreadyCompleted ? Results.Ok(new AdminAlreadyCompletedResponse(true)) : Results.NoContent();
        }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (KeyNotFoundException) { return NotFound(id); }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> StatusAsync(string rawId, Func<OverlayChannel, bool> mutation, string action, IOverlayChannelStore store, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken cancellationToken)
    {
        if (!TryCreateId(rawId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            OverlayChannel? channel = null;
            await coordinator.ExecuteAuditedAsync(
                async (connection, transaction, token) => channel = await store.MutateAsync(id, mutation, connection, transaction, cancellationToken: token).ConfigureAwait(false),
                () => NormalAudit(context, action, id.Value.ToString("D"), new Dictionary<string, string?> { ["Changed"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return channel is null ? NotFound(id) : Results.NoContent();
        }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> RotateAsync(
        string channelId,
        [FromBody] AdminActionRequest? request,
        RotateOverlaySourceKey useCase,
        AdminMutationCoordinator coordinator,
        IAdminExecutionContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        if (!TryHighRiskRequest(request, out var requestData, out var requestError)) return InvalidRequest(requestError!);
        var context = contextAccessor.Current;
        if (context is null) return Results.Unauthorized();
        try
        {
            OverlayChannelSecret? result = null;
            var mutation = await coordinator.ExecuteAsync(
                    new AdminMutationCommand(
                        requestData.RequestId,
                        context.ActorCommunityIdentityId,
                        AdminAuditActions.SourceKeyRotated,
                        "OverlayChannel",
                        id.Value.ToString("D"),
                        AdminRequestFingerprint.Compute(("channel", id.Value), ("reason", requestData.Reason)),
                        context.CorrelationId,
                        DateTimeOffset.UtcNow),
                    async (connection, transaction, token) =>
                    {
                        result = await useCase.ExecuteAsync(id, connection, transaction, token).ConfigureAwait(false);
                        if (result is null) throw new KeyNotFoundException();
                    },
                    () => CreateAudit(context, AdminAuditActions.SourceKeyRotated, id.Value.ToString("D"), requestData.Reason, requestData.RequestId, new Dictionary<string, string?> { ["SourceKeyRotated"] = "true" }),
                    cancellationToken).ConfigureAwait(false);
            if (mutation.AlreadyCompleted) return Results.Conflict(new AdminErrorResponse("Die RequestId wurde bereits verarbeitet; für eine neue Source-Key-Ausgabe ist eine neue RequestId erforderlich."));
            return Results.Ok(new OverlayChannelSecretResponse(ToResponse(result!.Channel), result.SourceKey, BrowserSourceUrl(result.SourceKey)));
        }
        catch (AdminOperationConflictException exception) { return Results.Conflict(new AdminErrorResponse(exception.Message)); }
        catch (KeyNotFoundException) { return NotFound(id); }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static bool TryHighRiskRequest(AdminActionRequest? request, out (Guid RequestId, string Reason) value, out string? error)
    {
        value = default;
        try
        {
            if (request?.RequestId is not Guid requestId || requestId == Guid.Empty)
            {
                throw new ArgumentException("Eine eindeutige RequestId ist erforderlich.");
            }

            value = (requestId, AdminReason.Required(request.Reason));
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static async Task<IResult> PreviewAsync(string channelId, [FromBody] OverlayAlertRequest? request, PublishPreviewAlert useCase, AdminMutationCoordinator coordinator, IAdminExecutionContextAccessor contextAccessor, CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var context = contextAccessor.Current;
            if (context is null) return Results.Unauthorized();
            var previewRequest = new OverlayAlertPublishRequest(
                id,
                request.Title!,
                request.Message,
                request.Variant ?? OverlayAlertVariant.Default,
                request.DurationMilliseconds ?? OverlayAlertDurationRules.DefaultMilliseconds,
                request.SourceType,
                request.SourceId);
            var publish = await coordinator.ExecuteAuditedAsync(
                (connection, transaction, token) => useCase.ExecuteAsync(previewRequest, connection, transaction, token),
                () => NormalAudit(context, AdminAuditActions.PreviewPublished, id.Value.ToString("D"), new Dictionary<string, string?> { ["Published"] = "true" }),
                cancellationToken).ConfigureAwait(false);
            return publish.Status switch
            {
                OverlayAlertPublishStatus.Published => Results.Ok(new OverlayAlertPublishResponse(publish.Status.ToString(), publish.AlertId)),
                OverlayAlertPublishStatus.ChannelNotFound => NotFound(id),
                OverlayAlertPublishStatus.ChannelArchived => Conflict("Der archivierte Overlay-Channel kann keine Preview-Alerts erzeugen."),
                _ => Results.Ok(new OverlayAlertPublishResponse(publish.Status.ToString(), null))
            };
        }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> BrowserSourceAsync(string sourceKey, ResolveBrowserSource useCase, HttpResponse response, CancellationToken cancellationToken)
    {
        var resolution = await useCase.ExecuteAsync(sourceKey, cancellationToken).ConfigureAwait(false);
        if (resolution is null) return Results.NotFound();
        var cursor = OverlayAlertCursorCodec.Encode(resolution.StartCursor);
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; frame-ancestors *";
        response.Headers["Referrer-Policy"] = "no-referrer";
        return Results.Content(BrowserHtml(cursor), "text/html; charset=utf-8");
    }

    private static async Task<IResult> StreamAsync(
        string sourceKey,
        HttpContext context,
        IOverlayChannelStore channelStore,
        IOverlayAlertStore alertStore,
        ReadAlertsAfterCursor readAfter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var channel = await channelStore.ResolveBySourceKeyAsync(sourceKey, cancellationToken).ConfigureAwait(false);
        if (channel is null) return Results.NotFound();

        var lastEventId = context.Request.Headers["Last-Event-ID"].FirstOrDefault();
        var requestedCursor = lastEventId ?? context.Request.Query["after"].FirstOrDefault();
        OverlayAlertCursor cursor;
        if (requestedCursor is null)
        {
            cursor = await alertStore.ReadTailAsync(channel.Id, cancellationToken).ConfigureAwait(false);
        }
        else if (!OverlayAlertCursorCodec.TryDecode(requestedCursor, channel.Id, out cursor, out var error))
        {
            return InvalidRequest(error);
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        var stopwatch = Stopwatch.StartNew();
        await context.Response.StartAsync(cancellationToken).ConfigureAwait(false);
        await context.Response.WriteAsync(": connected\n\n", cancellationToken).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var alerts = await readAfter.ExecuteAsync(channel.Id, cursor, Canonicalize(clock.UtcNow), OverlayTransportDefaults.MaxBatchSize, cancellationToken).ConfigureAwait(false);
                foreach (var alert in alerts)
                {
                    cursor = OverlayAlertCursor.Create(channel.Id, alert.CreatedAtUtc, alert.Id.Value);
                    await WriteEventAsync(context.Response, OverlayAlertCursorCodec.Encode(cursor), alert, cancellationToken).ConfigureAwait(false);
                }

                if (stopwatch.Elapsed >= OverlayTransportDefaults.HeartbeatInterval)
                {
                    await context.Response.WriteAsync(": heartbeat\n\n", cancellationToken).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stopwatch.Restart();
                }

                await Task.Delay(OverlayTransportDefaults.PollIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ein normaler Browser-/OBS-Disconnect ist kein Serverfehler.
        }

        return Results.Empty;
    }

    private static async Task WriteEventAsync(HttpResponse response, string cursor, OverlayAlert alert, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(
            new SseAlertPayload(
                alert.Id.Value,
                alert.ChannelId.Value,
                alert.Title,
                alert.Message,
                alert.Variant,
                alert.DurationMilliseconds,
                alert.SourceReference?.SourceType,
                alert.SourceReference?.SourceId,
                alert.CreatedAtUtc,
                alert.ExpiresAtUtc),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await response.WriteAsync($"id: {cursor}\nevent: overlay-alert\ndata: {data}\n\n", cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OverlayChannelResponse ToResponse(OverlayChannel channel) => new(channel.Id.Value, channel.DisplayName, channel.Description, channel.IsEnabled, channel.IsArchived, channel.CreatedAtUtc, channel.UpdatedAtUtc);
    private static string BrowserSourceUrl(string sourceKey) => $"/overlay/{Uri.EscapeDataString(sourceKey)}";
    private static bool TryCreateId(string raw, out OverlayChannelId id)
    {
        id = default;
        return Guid.TryParse(raw, out var value) && value != Guid.Empty && TryCreate(value, out id);
    }
    private static bool TryCreate(Guid value, out OverlayChannelId id)
    {
        try { id = OverlayChannelId.Create(value); return true; }
        catch (ArgumentException) { id = default; return false; }
    }
    private static DateTimeOffset Canonicalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
    private static IResult InvalidRequest(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültige Anfrage.", detail: detail);
    private static IResult NotFound(OverlayChannelId id) => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Overlay-Channel nicht gefunden.", detail: $"Der Overlay-Channel '{id.Value}' wurde nicht gefunden.");
    private static IResult Conflict(string detail) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Overlay-Channel-Konflikt.", detail: detail);

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
            context.ActorCommunityIdentityId.Value.ToString("D"),
            action,
            "OverlayChannel",
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

    private static AdminAuditEntry NormalAudit(
        AdminExecutionContext context,
        string action,
        string targetId,
        IReadOnlyDictionary<string, string?> changeSummary) =>
        new(
            Guid.NewGuid(),
            context.ActorCommunityIdentityId,
            context.ActorCommunityIdentityId.Value.ToString("D"),
            action,
            "OverlayChannel",
            targetId,
            null,
            AdminRiskLevel.Medium,
            null,
            AdminAuditOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            context.CorrelationId,
            null,
            null,
            changeSummary,
            new Dictionary<string, string?>());

    private static string BrowserHtml(string startCursor)
    {
        var cursorJson = JsonSerializer.Serialize(startCursor);
        return $$"""
            <!doctype html>
            <html lang="de">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <style>
                :root { color-scheme: dark; }
                html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: transparent; font-family: system-ui, sans-serif; }
                #alert { position: fixed; left: 50%; top: 12%; width: min(80vw, 900px); transform: translate(-50%, -20px); opacity: 0; padding: 28px 34px; border-radius: 18px; color: white; text-align: center; box-sizing: border-box; transition: opacity .35s ease, transform .35s ease; background: rgba(20,20,28,.94); border: 2px solid rgba(255,255,255,.22); }
                #alert.visible { opacity: 1; transform: translate(-50%, 0); }
                #alert.hiding { opacity: 0; transform: translate(-50%, -20px); }
                #alert[data-variant="success"] { border-color: #43d17c; } #alert[data-variant="warning"] { border-color: #f2be4b; } #alert[data-variant="celebration"] { border-color: #d67cff; }
                #title { margin: 0; font-size: clamp(24px, 4vw, 52px); line-height: 1.08; } #message { margin: 12px 0 0; font-size: clamp(16px, 2vw, 28px); white-space: pre-wrap; }
              </style>
            </head>
            <body>
              <section id="alert" aria-live="polite"><h1 id="title"></h1><p id="message"></p></section>
              <script>
                (() => {
                  const startCursor = {{cursorJson}};
                  const queue = []; const seen = new Set(); const maxSeen = 256; let busy = false;
                  const box = document.getElementById('alert'); const title = document.getElementById('title'); const message = document.getElementById('message');
                  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
                  async function showNext() {
                    if (busy || queue.length === 0) return; busy = true; const alert = queue.shift();
                    box.dataset.variant = alert.variant; title.textContent = alert.title; message.textContent = alert.message || ''; message.hidden = !alert.message;
                    box.classList.remove('hiding'); box.classList.add('visible'); await sleep(alert.durationMilliseconds); box.classList.remove('visible'); box.classList.add('hiding'); await sleep(380); busy = false; showNext();
                  }
                  function enqueue(alert) { if (!alert || !alert.id || seen.has(alert.id)) return; seen.add(alert.id); if (seen.size > maxSeen) seen.delete(seen.values().next().value); queue.push(alert); showNext(); }
                  const sourceKey = location.pathname.split('/').filter(Boolean).pop();
                  const stream = new EventSource('/api/overlay/sources/' + encodeURIComponent(sourceKey) + '/stream?after=' + encodeURIComponent(startCursor));
                  stream.addEventListener('overlay-alert', event => { try { enqueue(JSON.parse(event.data)); } catch (_) { /* invalid event is ignored */ } });
                })();
              </script>
            </body>
            </html>
            """;
    }

    private sealed record SseAlertPayload(
        Guid Id,
        Guid ChannelId,
        string Title,
        string? Message,
        string Variant,
        int DurationMilliseconds,
        string? SourceType,
        string? SourceId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc);
}
