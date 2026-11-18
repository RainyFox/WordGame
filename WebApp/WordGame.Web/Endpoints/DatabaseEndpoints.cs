using Microsoft.Extensions.Options;
using WordGame.Web.Data;
using WordGame.Web.Models;

namespace WordGame.Web.Endpoints;

public static class DatabaseEndpoints
{
    const string SourceHeader = "X-WordGame-Database";

    public static IEndpointRouteBuilder MapDatabaseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/database/options", (WordGameDatabaseSelection selection) =>
            Results.Ok(new { selection.DefaultSource, selection.TursoConfigured }));
        endpoints.MapGet("/api/health", GetHealthAsync);
        endpoints.MapGet("/api/vocabulary/summary", GetSummaryAsync);
        return endpoints;
    }

    public static async Task SelectDatabaseAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        context.Response.Headers.Append("Vary", SourceHeader);
        if (context.Request.Headers.TryGetValue(SourceHeader, out var header))
        {
            WordGameDatabaseSource? source = header.ToString() switch
            {
                "Local" => WordGameDatabaseSource.Local,
                "Turso" => WordGameDatabaseSource.Turso,
                _ => null
            };
            if (source is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new ApiErrorResponse("資料庫選項無效。"));
                return;
            }

            var selection = context.RequestServices.GetRequiredService<WordGameDatabaseSelection>();
            if (source == WordGameDatabaseSource.Turso && !selection.TursoConfigured)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new ApiErrorResponse("尚未設定雲端 Turso 資料庫。"));
                return;
            }

            selection.Source = source.Value;
        }

        await next(context);
    }

    static async Task<IResult> GetHealthAsync(
        IVocabularyReadRepository repository,
        IWordGameDatabasePathResolver pathResolver,
        IOptions<WordGameDatabaseOptions> options,
        WordGameDatabaseSelection selection,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        bool usesTurso = selection.Source == WordGameDatabaseSource.Turso;
        try
        {
            VocabularySummary summary = await repository.GetSummaryAsync(cancellationToken);
            return Results.Ok(new HealthResponse(
                "ok",
                usesTurso ? options.Value.TursoUrl! : pathResolver.GetDatabasePath(),
                summary.Count));
        }
        catch (Exception exception)
        {
            if (usesTurso)
            {
                logger.LogWarning("WordGame could not read the Turso database ({ErrorType}).",
                    exception.GetType().Name);
                return Results.Json(
                    new ApiErrorResponse("無法連接 Turso 資料庫。請確認網路及連線設定。"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            logger.LogError(exception, "WordGame database health check failed.");
            if (SqliteDatabaseErrors.IsBusyOrLocked(exception))
            {
                return Results.Json(
                    new ApiErrorResponse(
                        "WordGame.db 正在被其他程式使用。請關閉 Unity 或 SQLite Browser 後再試。"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

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
        catch (Exception exception)
        {
            if (SqliteDatabaseErrors.IsBusyOrLocked(exception))
            {
                return Results.Json(
                    new ApiErrorResponse(
                        "WordGame.db 正在被其他程式使用。請關閉 Unity 或 SQLite Browser 後再試。"),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Json(
                new ApiErrorResponse("無法讀取單字摘要。"),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
