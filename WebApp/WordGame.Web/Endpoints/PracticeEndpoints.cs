using WordGame.Web.Data;
using WordGame.Web.Models;
using WordGame.Web.Services;

namespace WordGame.Web.Endpoints;

public static class PracticeEndpoints
{
    public static IEndpointRouteBuilder MapPracticeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/practice");
        group.MapGet("/options", GetOptionsAsync);
        group.MapPost("/sessions", StartSessionAsync);
        group.MapPost("/sessions/{sessionId:guid}/answer", SubmitAnswerAsync);
        group.MapPost("/sessions/{sessionId:guid}/reveal", RevealAnswerAsync);
        group.MapPost("/sessions/{sessionId:guid}/next", GetNextQuestionAsync);
        group.MapDelete("/sessions/{sessionId:guid}", EndSession);
        return endpoints;
    }

    static async Task<IResult> GetOptionsAsync(
        IPracticeVocabularyRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await repository.GetOptionsAsync(cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    static async Task<IResult> StartSessionAsync(
        StartPracticeRequest request,
        IPracticeSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.StartAsync(request, cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    static async Task<IResult> SubmitAnswerAsync(
        Guid sessionId,
        SubmitPracticeAnswerRequest request,
        IPracticeSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.SubmitAnswerAsync(
                sessionId,
                request,
                cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    static async Task<IResult> RevealAnswerAsync(
        Guid sessionId,
        IPracticeSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.RevealAnswerAsync(
                sessionId,
                cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    static async Task<IResult> GetNextQuestionAsync(
        Guid sessionId,
        IPracticeSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetNextQuestionAsync(
                sessionId,
                cancellationToken));
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    static async Task<IResult> EndSession(
        Guid sessionId,
        IPracticeSessionService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.EndAsync(sessionId, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    static IResult MapException(Exception exception)
    {
        return exception switch
        {
            PracticeValidationException => Results.BadRequest(
                new ApiErrorResponse(exception.Message)),
            PracticeSessionNotFoundException => Results.NotFound(
                new ApiErrorResponse(exception.Message)),
            PracticeStateException => Results.Conflict(
                new ApiErrorResponse(exception.Message)),
            _ => Results.Json(
                new ApiErrorResponse("無法讀取練習資料。"),
                statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }
}
