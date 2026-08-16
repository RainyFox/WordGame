using Microsoft.Extensions.Caching.Memory;
using WordGame.Web.Data;
using WordGame.Web.Models;

namespace WordGame.Web.Services;

public sealed class PracticeSessionService(
    IPracticeVocabularyRepository vocabularyRepository,
    IUserProgressRepository progressRepository,
    IRandomSource randomSource,
    TimeProvider timeProvider,
    IMemoryCache cache) : IPracticeSessionService
{
    static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public async Task<StartPracticeResponse> StartAsync(
        StartPracticeRequest request,
        CancellationToken cancellationToken)
    {
        VocabularyFilter filter = CreateFilter(request);
        IReadOnlyList<int>? fullRandomCandidates = request.Mode == PracticeMode.FullRandom
            ? await LoadFullRandomCandidatesAsync(filter, cancellationToken)
            : null;
        var session = new PracticeSession(
            request,
            filter,
            fullRandomCandidates,
            randomSource);
        Guid sessionId = Guid.NewGuid();
        cache.Set(sessionId, session, SessionLifetime);

        try
        {
            PracticeQuestionResponse question = await MoveToNextQuestionAsync(
                session,
                cancellationToken);
            return new StartPracticeResponse(sessionId, question);
        }
        catch
        {
            cache.Remove(sessionId);
            throw;
        }
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
        {
            session.RegisterWrongAttempt(number);
            return new PracticeAnswerResponse(false, null, null, null);
        }

        return await SettleQuestionAsync(
            session,
            vocabulary,
            currentAnswerIsCorrect: true,
            cancellationToken);
    }

    public async Task<PracticeAnswerResponse> RevealAnswerAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        PracticeSession session = GetSession(sessionId);
        int number = session.GetCurrentUnansweredNumber();
        VocabularyEntry vocabulary = await GetVocabularyAsync(number, cancellationToken);
        return await SettleQuestionAsync(
            session,
            vocabulary,
            currentAnswerIsCorrect: false,
            cancellationToken);
    }

    public async Task EndAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetSession(sessionId, out PracticeSession? session) || session is null)
            return;

        PracticeSettlement? settlement = session.BeginAbandonmentSettlement();
        if (settlement is not null)
            await RecordSettlementAsync(session, settlement, cancellationToken);
        cache.Remove(sessionId);
    }

    async Task<IReadOnlyList<int>> LoadFullRandomCandidatesAsync(
        VocabularyFilter filter,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> candidates = await vocabularyRepository.GetCandidateNumbersAsync(
            filter,
            cancellationToken);
        EnsureCandidatesExist(candidates.Count);
        return candidates;
    }

    async Task<PracticeQuestionResponse> MoveToNextQuestionAsync(
        PracticeSession session,
        CancellationToken cancellationToken)
    {
        PracticeQuestionPosition position = session.Mode == PracticeMode.FullRandom
            ? session.BeginNextFullRandomQuestion()
            : await SelectNextProficiencyQuestionAsync(session, cancellationToken);
        VocabularyEntry vocabulary = await GetVocabularyAsync(
            position.Number,
            cancellationToken);
        string prompt = session.Direction == PracticeDirection.JpToCn
            ? vocabulary.Word
            : vocabulary.Chinese;

        return new PracticeQuestionResponse(
            vocabulary.Number,
            prompt,
            session.Mode,
            position.Round,
            position.Position,
            position.Total);
    }

    async Task<PracticeQuestionPosition> SelectNextProficiencyQuestionAsync(
        PracticeSession session,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ReviewCandidateProgress> progressRecords =
            await progressRepository.GetReviewCandidatesAsync(
                session.Filter,
                session.Direction,
                cancellationToken);
        EnsureCandidatesExist(progressRecords.Count);

        IReadOnlyList<ReviewCandidate> candidates = ReviewCandidateCalculator.Calculate(
            progressRecords,
            timeProvider.GetUtcNow());
        ReviewCandidate selected = ProficiencyQuestionSelector.Select(
            candidates,
            randomSource.NextDouble(),
            randomSource.NextDouble());
        return session.BeginNextProficiencyQuestion(selected.Number, candidates.Count);
    }

    async Task<PracticeAnswerResponse> SettleQuestionAsync(
        PracticeSession session,
        VocabularyEntry vocabulary,
        bool currentAnswerIsCorrect,
        CancellationToken cancellationToken)
    {
        PracticeSettlement settlement = session.BeginSettlement(
            vocabulary.Number,
            currentAnswerIsCorrect);
        UserProgressRecord progress = await RecordSettlementAsync(
            session,
            settlement,
            cancellationToken);
        return new PracticeAnswerResponse(
            currentAnswerIsCorrect,
            settlement.Outcome,
            CreateReveal(session.Direction, vocabulary),
            CreateProgressResponse(progress));
    }

    async Task<UserProgressRecord> RecordSettlementAsync(
        PracticeSession session,
        PracticeSettlement settlement,
        CancellationToken cancellationToken)
    {
        try
        {
            UserProgressRecord progress = await progressRepository.RecordAnswerAsync(
                settlement.Number,
                session.Direction,
                settlement.Outcome == PracticeOutcome.Correct,
                timeProvider.GetUtcNow(),
                cancellationToken);
            session.CompleteSettlement(settlement.Number);
            return progress;
        }
        catch
        {
            session.CancelSettlement(settlement.Number);
            throw;
        }
    }

    async Task<VocabularyEntry> GetVocabularyAsync(
        int number,
        CancellationToken cancellationToken)
    {
        VocabularyEntry? vocabulary = await vocabularyRepository.GetByNumberAsync(
            number,
            cancellationToken);
        return vocabulary ?? throw new PracticeStateException(
            $"番号 {number} 的單字已不存在。");
    }

    PracticeSession GetSession(Guid sessionId)
    {
        if (!TryGetSession(sessionId, out PracticeSession? session) || session is null)
            throw new PracticeSessionNotFoundException();
        return session;
    }

    bool TryGetSession(Guid sessionId, out PracticeSession? session)
    {
        return cache.TryGetValue(sessionId, out session) && session is not null;
    }

    static VocabularyFilter CreateFilter(StartPracticeRequest request)
    {
        if (request.MinNumber > request.MaxNumber)
            throw new PracticeValidationException("起始番号不能大於結束番号。");
        if (!Enum.IsDefined(request.Mode))
            throw new PracticeValidationException("練習模式無效。");
        if (!Enum.IsDefined(request.Direction))
            throw new PracticeValidationException("翻譯方向無效。");

        string? type = string.IsNullOrWhiteSpace(request.Type)
            ? null
            : request.Type.Trim();
        return new VocabularyFilter(request.MinNumber, request.MaxNumber, type);
    }

    static void EnsureCandidatesExist(int candidateCount)
    {
        if (candidateCount == 0)
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

    static UserProgressResponse CreateProgressResponse(UserProgressRecord progress)
    {
        return new UserProgressResponse(
            progress.Proficiency,
            progress.LastAnswer,
            progress.NextReview,
            progress.TotalCorrect,
            progress.TotalWrong);
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
