using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class WordGameWebApplicationTests
{
    [Fact]
    public async Task HealthAndSummaryEndpoints_ReadConfiguredDatabase()
    {
        using var database = new TemporaryWordGameDatabase();
        byte[] hashBeforeRequests = database.ComputeHash();
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(database.DatabasePath);
        using HttpClient client = factory.CreateClient();

        HealthResponse? health = await client.GetFromJsonAsync<HealthResponse>(
            "/api/health",
            CancellationToken.None);
        VocabularySummary? summary = await client.GetFromJsonAsync<VocabularySummary>(
            "/api/vocabulary/summary",
            CancellationToken.None);

        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal(Path.GetFullPath(database.DatabasePath), health.DatabasePath);
        Assert.Equal(4, health.VocabularyCount);
        Assert.NotNull(summary);
        Assert.Equal(4, summary.Count);
        Assert.Equal(hashBeforeRequests, database.ComputeHash());
    }

    [Fact]
    public async Task IndexPage_IsServedByApplication()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(database.DatabasePath);
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync(
            "/",
            CancellationToken.None);

        Assert.Contains("WordGame Web", html, StringComparison.Ordinal);
        Assert.Contains("READ ONLY", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsServiceUnavailableForMissingDatabase()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(missingPath);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/health",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
