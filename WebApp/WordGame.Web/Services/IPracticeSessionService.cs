using WordGame.Web.Models;

namespace WordGame.Web.Services;

public interface IPracticeSessionService
{
    Task<StartPracticeResponse> StartAsync(
        StartPracticeRequest request,
        CancellationToken cancellationToken);

    Task<PracticeQuestionResponse> GetNextQuestionAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<PracticeAnswerResponse> SubmitAnswerAsync(
        Guid sessionId,
        SubmitPracticeAnswerRequest request,
        CancellationToken cancellationToken);

    Task<PracticeAnswerResponse> RevealAnswerAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task EndAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}
