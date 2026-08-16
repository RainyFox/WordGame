using WordGame.Web.Data;
using WordGame.Web.Models;

namespace WordGame.Web.Endpoints;

public static class DatabaseEndpoints
{
    public static IEndpointRouteBuilder MapDatabaseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/health", GetHealthAsync);
        endpoints.MapGet("/api/vocabulary/summary", GetSummaryAsync);
        return endpoints;
    }

    static async Task<IResult> GetHealthAsync(
        IVocabularyReadRepository repository,
        IWordGameDatabasePathResolver pathResolver,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            VocabularySummary summary = await repository.GetSummaryAsync(cancellationToken);
            return Results.Ok(new HealthResponse(
                "ok",
                pathResolver.GetDatabasePath(),
                summary.Count));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "WordGame database health check failed.");
            return Results.Json(
                new ApiErrorResponse("無法以唯讀模式連接 WordGame.db。"),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    static async Task<IResult> GetSummaryAsync(
        IVocabularyReadRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await repository.GetSummaryAsync(cancellationToken));
        }
        catch
        {
            return Results.Json(
                new ApiErrorResponse("無法讀取單字摘要。"),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
