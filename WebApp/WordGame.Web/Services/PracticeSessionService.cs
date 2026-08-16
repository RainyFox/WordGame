using Microsoft.Extensions.Caching.Memory;
using WordGame.Web.Data;
using WordGame.Web.Models;

namespace WordGame.Web.Services;

public sealed class PracticeSessionService(
    IPracticeVocabularyRepository repository,
    IMemoryCache cache) : IPracticeSessionService
{
    static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public async Task<StartPracticeResponse> StartAsync(
        StartPracticeRequest request,
        CancellationToken cancellationToken)
    {
        VocabularyFilter filter = CreateFilter(request);
        IReadOnlyList<int> candidates = await repository.GetCandidateNumbersAsync(
            filter,
            cancellationToken);
        EnsureCandidatesExist(candidates);

        var session = new PracticeSession(request.Direction, candidates);
        Guid sessionId = Guid.NewGuid();
        cache.Set(sessionId, session, SessionLifetime);

        PracticeQuestionResponse question = await MoveToNextQuestionAsync(
            session,
            cancellationToken);
        return new StartPracticeResponse(sessionId, question);
    }

    public Task<PracticeQuestionResponse> GetNextQuestionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return MoveToNextQuestionAsync(GetSession(sessionId), cancellationToken);
    }

    public async Task<PracticeAnswerResponse> SubmitAnswerAsync(
        Guid sessionId,
        SubmitPracticeAnswerRequest request,
        CancellationToken cancellationToken)
    {
        PracticeSession session = GetSession(sessionId);
        int number = session.GetCurrentUnansweredNumber();
        VocabularyEntry vocabulary = await GetVocabularyAsync(number, cancellationToken);
        bool isCorrect = IsCorrectAnswer(session.Direction, vocabulary, request.Answer);

        if (!isCorrect)
            return new PracticeAnswerResponse(false, null);

        session.MarkCurrentQuestionAnswered(number);
        return new PracticeAnswerResponse(
            true,
            CreateReveal(session.Direction, vocabulary));
    }

    public async Task<PracticeAnswerResponse> RevealAnswerAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        PracticeSession session = GetSession(sessionId);
        int number = session.GetCurrentUnansweredNumber();
        VocabularyEntry vocabulary = await GetVocabularyAsync(number, cancellationToken);
        session.MarkCurrentQuestionAnswered(number);
        return new PracticeAnswerResponse(
            false,
            CreateReveal(session.Direction, vocabulary));
    }

    public void End(Guid sessionId)
    {
        cache.Remove(sessionId);
    }

    async Task<PracticeQuestionResponse> MoveToNextQuestionAsync(
        PracticeSession session,
        CancellationToken cancellationToken)
    {
        PracticeQuestionPosition position = session.MoveToNextQuestion();
        VocabularyEntry vocabulary = await GetVocabularyAsync(
            position.Number,
            cancellationToken);
        string prompt = session.Direction == PracticeDirection.JpToCn
            ? vocabulary.Word
            : vocabulary.Chinese;

        return new PracticeQuestionResponse(
            vocabulary.Number,
            prompt,
            position.Round,
            position.Position,
            position.Total);
    }

    async Task<VocabularyEntry> GetVocabularyAsync(
        int number,
        CancellationToken cancellationToken)
    {
        VocabularyEntry? vocabulary = await repository.GetByNumberAsync(
            number,
            cancellationToken);
        return vocabulary ?? throw new PracticeStateException(
            $"番号 {number} 的單字已不存在。");
    }

    PracticeSession GetSession(Guid sessionId)
    {
        if (!cache.TryGetValue(sessionId, out PracticeSession? session) || session is null)
            throw new PracticeSessionNotFoundException();
        return session;
    }

    static VocabularyFilter CreateFilter(StartPracticeRequest request)
    {
        if (request.MinNumber > request.MaxNumber)
            throw new PracticeValidationException("起始番号不能大於結束番号。");
        if (!Enum.IsDefined(request.Direction))
            throw new PracticeValidationException("翻譯方向無效。");

        string? type = string.IsNullOrWhiteSpace(request.Type)
            ? null
            : request.Type.Trim();
        return new VocabularyFilter(request.MinNumber, request.MaxNumber, type);
    }

    static void EnsureCandidatesExist(IReadOnlyCollection<int> candidates)
    {
        if (candidates.Count == 0)
            throw new PracticeValidationException("指定範圍與類型內沒有單字。");
    }

    static bool IsCorrectAnswer(
        PracticeDirection direction,
        VocabularyEntry vocabulary,
        string? answer)
    {
        string expectedAnswer = GetExpectedAnswer(direction, vocabulary);
        return string.Equals(answer, expectedAnswer, StringComparison.Ordinal);
    }

    static PracticeRevealResponse CreateReveal(
        PracticeDirection direction,
        VocabularyEntry vocabulary)
    {
        string translation = direction == PracticeDirection.JpToCn
            ? vocabulary.Chinese
            : vocabulary.Kana;
        return new PracticeRevealResponse(
            GetExpectedAnswer(direction, vocabulary),
            translation,
            RemoveParentheses(vocabulary.Example));
    }

    static string GetExpectedAnswer(
        PracticeDirection direction,
        VocabularyEntry vocabulary)
    {
        return direction == PracticeDirection.JpToCn
            ? vocabulary.Kana
            : vocabulary.Word;
    }

    static string RemoveParentheses(string text)
    {
        return text.Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal);
    }
}
