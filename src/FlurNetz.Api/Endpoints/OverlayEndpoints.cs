using System.Diagnostics;
using System.Text.Json;
using FlurNetz.Api.Contracts;
using FlurNetz.Api.Cursors;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Api.Endpoints;

/// <summary>Ordnet Overlay-Management, Browser Source und SSE der API-Grenze zu.</summary>
public static class OverlayEndpoints
{
    private const string ChannelsRoute = "/api/admin/overlay/channels";

    /// <summary>Registriert die interne Management-Grenze und den OBS-Transport.</summary>
    public static IEndpointRouteBuilder MapOverlayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(ChannelsRoute, ListAsync);
        endpoints.MapGet($"{ChannelsRoute}/{{channelId}}", GetAsync);
        endpoints.MapPost(ChannelsRoute, CreateAsync);
        endpoints.MapPut($"{ChannelsRoute}/{{channelId}}", UpdateAsync);
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/enable", EnableAsync);
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/disable", DisableAsync);
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/archive", ArchiveAsync);
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/rotate-source-key", RotateAsync);
        endpoints.MapPost($"{ChannelsRoute}/{{channelId}}/alerts", PreviewAsync);
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

    private static async Task<IResult> CreateAsync(OverlayChannelRequest? request, CreateOverlayChannel useCase, CancellationToken cancellationToken)
    {
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var result = await useCase.ExecuteAsync(request.DisplayName!, request.Description, cancellationToken).ConfigureAwait(false);
            return Results.Created($"{ChannelsRoute}/{result.Channel.Id.Value:D}", new OverlayChannelSecretResponse(ToResponse(result.Channel), result.SourceKey, BrowserSourceUrl(result.SourceKey)));
        }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> UpdateAsync(string channelId, OverlayChannelRequest? request, UpdateOverlayChannelMetadata useCase, CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var channel = await useCase.ExecuteAsync(id, request.DisplayName!, request.Description, cancellationToken).ConfigureAwait(false);
            return channel is null ? NotFound(id) : Results.Ok(ToResponse(channel));
        }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static Task<IResult> EnableAsync(string channelId, EnableOverlayChannel useCase, CancellationToken cancellationToken) => StatusAsync(channelId, useCase.ExecuteAsync, cancellationToken);
    private static Task<IResult> DisableAsync(string channelId, DisableOverlayChannel useCase, CancellationToken cancellationToken) => StatusAsync(channelId, useCase.ExecuteAsync, cancellationToken);
    private static Task<IResult> ArchiveAsync(string channelId, ArchiveOverlayChannel useCase, CancellationToken cancellationToken) => StatusAsync(channelId, useCase.ExecuteAsync, cancellationToken);

    private static async Task<IResult> StatusAsync(string rawId, Func<OverlayChannelId, CancellationToken, Task<OverlayChannel?>> operation, CancellationToken cancellationToken)
    {
        if (!TryCreateId(rawId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        try
        {
            return await operation(id, cancellationToken).ConfigureAwait(false) is null ? NotFound(id) : Results.NoContent();
        }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> RotateAsync(string channelId, RotateOverlaySourceKey useCase, CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        try
        {
            var result = await useCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
            return result is null
                ? NotFound(id)
                : Results.Ok(new OverlayChannelSecretResponse(ToResponse(result.Channel), result.SourceKey, BrowserSourceUrl(result.SourceKey)));
        }
        catch (OverlayChannelArchivedException exception) { return Conflict(exception.Message); }
        catch (ArgumentException exception) { return InvalidRequest(exception.Message); }
    }

    private static async Task<IResult> PreviewAsync(string channelId, OverlayAlertRequest? request, PublishPreviewAlert useCase, CancellationToken cancellationToken)
    {
        if (!TryCreateId(channelId, out var id)) return InvalidRequest("Die Route-ID des Overlay-Channels ist ungültig.");
        if (request is null) return InvalidRequest("Der Request-Body ist erforderlich.");
        try
        {
            var publish = await useCase.ExecuteAsync(new OverlayAlertPublishRequest(
                id,
                request.Title!,
                request.Message,
                request.Variant ?? OverlayAlertVariant.Default,
                request.DurationMilliseconds ?? OverlayAlertDurationRules.DefaultMilliseconds,
                request.SourceType,
                request.SourceId), cancellationToken).ConfigureAwait(false);
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
