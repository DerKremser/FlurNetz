using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FlurNetz.Api.Contracts;

namespace FlurNetz.Api.IntegrationTests;

/// <summary>Prüft Management-Grenze, Browser Source und SSE für Overlay V1.</summary>
public sealed class OverlayManagementApiPostgreSqlTests(ApiPostgreSqlFixture database)
    : IClassFixture<ApiPostgreSqlFixture>
{
    [Fact]
    public async Task CreateGetListPreviewAndBrowserSourceDoNotExposeSecretsOnReads()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestToken);
        using var host = new FlurNetzApiFactory(database.ConnectionString);
        using var client = host.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/overlay/channels",
            new OverlayChannelRequest("  OBS Alerts  ", "  Test  "),
            TestToken);
        var created = await createResponse.Content.ReadFromJsonAsync<OverlayChannelSecretResponse>(TestToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("OBS Alerts", created!.Channel.DisplayName);
        Assert.Equal(43, created.SourceKey.Length);
        Assert.Equal($"/api/admin/overlay/channels/{created.Channel.Id:D}", createResponse.Headers.Location?.OriginalString);
        Assert.Equal($"/overlay/{created.SourceKey}", created.BrowserSourceUrl);

        var route = $"/api/admin/overlay/channels/{created.Channel.Id:D}";
        using var getResponse = await client.GetAsync(route, TestToken);
        var getBody = await getResponse.Content.ReadAsStringAsync(TestToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.DoesNotContain(created.SourceKey, getBody, StringComparison.Ordinal);
        Assert.DoesNotContain("source_key_hash", getBody, StringComparison.OrdinalIgnoreCase);

        using var listResponse = await client.GetAsync("/api/admin/overlay/channels", TestToken);
        var listBody = await listResponse.Content.ReadAsStringAsync(TestToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(created.Channel.Id.ToString("D"), listBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(created.SourceKey, listBody, StringComparison.Ordinal);

        using var sourceResponse = await client.GetAsync(created.BrowserSourceUrl, TestToken);
        var sourceBody = await sourceResponse.Content.ReadAsStringAsync(TestToken);
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);
        Assert.Equal("no-referrer", sourceResponse.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("textContent", sourceBody, StringComparison.Ordinal);
        Assert.Contains("EventSource", sourceBody, StringComparison.Ordinal);

        using var previewResponse = await client.PostAsJsonAsync(
            $"{route}/alerts",
            new OverlayAlertRequest("Preview", "Works", "success", 1_000, null, null),
            TestToken);
        var preview = await previewResponse.Content.ReadFromJsonAsync<OverlayAlertPublishResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal("Published", preview?.Status);
        Assert.NotEqual(Guid.Empty, preview?.AlertId);
    }

    [Fact]
    public async Task RotateInvalidatesOldSourceAndArchiveIsTerminal()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestToken);
        using var host = new FlurNetzApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        var created = await CreateAsync(client);
        var route = $"/api/admin/overlay/channels/{created.Channel.Id:D}";

        using var rotateResponse = await client.PostAsync($"{route}/rotate-source-key", null, TestToken);
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<OverlayChannelSecretResponse>(TestToken);
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        Assert.NotNull(rotated);
        Assert.NotEqual(created.SourceKey, rotated!.SourceKey);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(created.BrowserSourceUrl, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(rotated.BrowserSourceUrl, TestToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{route}/archive", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(rotated.BrowserSourceUrl, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{route}/enable", null, TestToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{route}/alerts", new OverlayAlertRequest("Nope", null, null, null, null, null), TestToken)).StatusCode);
    }

    [Fact]
    public async Task StreamStartsAtTailAndDeliversSubsequentAlertsWithSseId()
    {
        SkipIfUnavailable();
        await database.ResetDatabaseAsync(TestToken);
        using var host = new FlurNetzApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        var created = await CreateAsync(client);
        var route = $"/api/admin/overlay/channels/{created.Channel.Id:D}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{route}/enable", null, TestToken)).StatusCode);
        var sourceHtml = await client.GetStringAsync(created.BrowserSourceUrl, TestToken);
        var cursor = Regex.Match(sourceHtml, @"const startCursor = ""([^""]+)""", RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(cursor);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var streamResponse = await client.GetAsync(
            $"/api/overlay/sources/{created.SourceKey}/stream?after={Uri.EscapeDataString(cursor)}",
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
        Assert.Equal("text/event-stream", streamResponse.Content.Headers.ContentType?.MediaType);

        using var reader = new StreamReader(await streamResponse.Content.ReadAsStreamAsync(timeout.Token));
        var publishTask = client.PostAsJsonAsync(
            $"{route}/alerts",
            new OverlayAlertRequest("Live", null, "celebration", 1_000, null, null),
            timeout.Token);
        var lines = new List<string>();
        while (lines.Count < 10 && !timeout.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(timeout.Token);
            if (line is null) break;
            lines.Add(line);
            if (line.StartsWith("data: ", StringComparison.Ordinal)) break;
        }

        var publishResponse = await publishTask;
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Contains(lines, line => line.StartsWith("id: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Live", StringComparison.Ordinal));
    }

    private async Task<OverlayChannelSecretResponse> CreateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/overlay/channels",
            new OverlayChannelRequest("OBS", null),
            TestToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OverlayChannelSecretResponse>(TestToken))!;
    }

    private void SkipIfUnavailable() => Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
}
