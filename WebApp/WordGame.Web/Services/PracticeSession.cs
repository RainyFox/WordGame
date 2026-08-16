using WordGame.Web.Models;

namespace WordGame.Web.Services;

internal sealed class PracticeSession
{
    readonly object syncRoot = new();
    readonly FullRandomQuestionDeck? fullRandomDeck;
    int? currentNumber;
    bool currentQuestionCompleted = true;
    bool hadWrongAttempt;
    bool settlementInProgress;

    public PracticeSession(
        StartPracticeRequest request,
        VocabularyFilter filter,
        IReadOnlyList<int>? fullRandomCandidates,
        IRandomSource randomSource)
    {
        Direction = request.Direction;
        Mode = request.Mode;
        Filter = filter;
        if (fullRandomCandidates is not null)
            fullRandomDeck = new FullRandomQuestionDeck(fullRandomCandidates, randomSource);
    }

    public PracticeDirection Direction { get; }

    public PracticeMode Mode { get; }

    public VocabularyFilter Filter { get; }

    public PracticeQuestionPosition BeginNextFullRandomQuestion()
    {
        lock (syncRoot)
        {
            EnsureReadyForNextQuestion();
            PracticeQuestionPosition position = fullRandomDeck?.MoveToNextQuestion()
                ?? throw new PracticeStateException("完全隨機題組尚未建立。");
            BeginQuestion(position.Number);
            return position;
        }
    }

    public PracticeQuestionPosition BeginNextProficiencyQuestion(
        int number,
        int candidateCount)
    {
        lock (syncRoot)
        {
            EnsureReadyForNextQuestion();
            BeginQuestion(number);
            return new PracticeQuestionPosition(number, 0, 0, candidateCount);
        }
    }

    public int GetCurrentUnansweredNumber()
    {
        lock (syncRoot)
        {
            EnsureCurrentQuestionCanChange();
            return currentNumber!.Value;
        }
    }

    public void RegisterWrongAttempt(int expectedNumber)
    {
        lock (syncRoot)
        {
            EnsureExpectedQuestionCanChange(expectedNumber);
            hadWrongAttempt = true;
        }
    }

    public PracticeSettlement BeginSettlement(
        int expectedNumber,
        bool currentAnswerIsCorrect)
    {
        lock (syncRoot)
        {
            EnsureExpectedQuestionCanChange(expectedNumber);
            settlementInProgress = true;
            PracticeOutcome outcome = currentAnswerIsCorrect && !hadWrongAttempt
                ? PracticeOutcome.Correct
                : PracticeOutcome.Wrong;
            return new PracticeSettlement(expectedNumber, outcome);
        }
    }

    public PracticeSettlement? BeginAbandonmentSettlement()
    {
        lock (syncRoot)
        {
            if (!currentNumber.HasValue || currentQuestionCompleted || !hadWrongAttempt)
                return null;

            EnsureCurrentQuestionCanChange();
            settlementInProgress = true;
            return new PracticeSettlement(currentNumber.Value, PracticeOutcome.Wrong);
        }
    }

    public void CompleteSettlement(int expectedNumber)
    {
        lock (syncRoot)
        {
            EnsureSettlementMatches(expectedNumber);
            settlementInProgress = false;
            currentQuestionCompleted = true;
        }
    }

    public void CancelSettlement(int expectedNumber)
    {
        lock (syncRoot)
        {
            EnsureSettlementMatches(expectedNumber);
            settlementInProgress = false;
        }
    }

    void BeginQuestion(int number)
    {
        currentNumber = number;
        currentQuestionCompleted = false;
        hadWrongAttempt = false;
        settlementInProgress = false;
    }

    void EnsureReadyForNextQuestion()
    {
        if (settlementInProgress)
            throw new PracticeStateException("正在儲存目前題目的結果。");
        if (currentNumber.HasValue && !currentQuestionCompleted)
            throw new PracticeStateException("請先答對或顯示目前題目的答案。");
    }

    void EnsureExpectedQuestionCanChange(int expectedNumber)
    {
        EnsureCurrentQuestionCanChange();
        if (currentNumber != expectedNumber)
            throw new PracticeStateException("題目狀態已變更，請重新載入。");
    }

    void EnsureCurrentQuestionCanChange()
    {
        if (!currentNumber.HasValue || currentQuestionCompleted)
            throw new PracticeStateException("目前沒有等待作答的題目。");
        if (settlementInProgress)
            throw new PracticeStateException("正在儲存目前題目的結果。");
    }

    void EnsureSettlementMatches(int expectedNumber)
    {
        if (!settlementInProgress || currentNumber != expectedNumber)
            throw new PracticeStateException("題目結算狀態已變更。");
    }
}

internal sealed record PracticeQuestionPosition(
    int Number,
    int Round,
    int Position,
    int Total);

internal sealed record PracticeSettlement(
    int Number,
    PracticeOutcome Outcome);
