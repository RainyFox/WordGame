using WordGame.Web.Data;
using WordGame.Web.Models;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5276");
builder.Services.Configure<WordGameDatabaseOptions>(
    builder.Configuration.GetSection(WordGameDatabaseOptions.SectionName));
builder.Services.AddSingleton<IWordGameDatabasePathResolver, WordGameDatabasePathResolver>();
builder.Services.AddSingleton<IVocabularyReadRepository, SqliteVocabularyReadRepository>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", async (
    IVocabularyReadRepository repository,
    IWordGameDatabasePathResolver pathResolver,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
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
});

app.MapGet("/api/vocabulary/summary", async (
    IVocabularyReadRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await repository.GetSummaryAsync(cancellationToken));
    }
    catch (Exception exception)
    {
        return Results.Problem(
            title: "Vocabulary summary is unavailable.",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
