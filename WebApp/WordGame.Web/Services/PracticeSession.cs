using WordGame.Web.Models;

namespace WordGame.Web.Services;

internal sealed class PracticeSession(
    PracticeDirection direction,
    IReadOnlyList<int> candidateNumbers)
{
    readonly object syncRoot = new();
    readonly List<int> candidateNumbers = [.. candidateNumbers];
    int nextIndex = candidateNumbers.Count;
    int round;
    int? currentNumber;
    bool currentAnswerRevealed = true;

    public PracticeDirection Direction { get; } = direction;

    public PracticeQuestionPosition MoveToNextQuestion()
    {
        lock (syncRoot)
        {
            EnsureReadyForNextQuestion();
            StartNextRoundIfNeeded();

            currentNumber = candidateNumbers[nextIndex];
            currentAnswerRevealed = false;
            nextIndex++;
            return new PracticeQuestionPosition(
                currentNumber.Value,
                round,
                nextIndex,
                candidateNumbers.Count);
        }
    }

    public int GetCurrentUnansweredNumber()
    {
        lock (syncRoot)
        {
            if (!currentNumber.HasValue || currentAnswerRevealed)
                throw new PracticeStateException("目前沒有等待作答的題目。");
            return currentNumber.Value;
        }
    }

    public void MarkCurrentQuestionAnswered(int expectedNumber)
    {
        lock (syncRoot)
        {
            if (currentNumber != expectedNumber || currentAnswerRevealed)
                throw new PracticeStateException("題目狀態已變更，請重新載入。");
            currentAnswerRevealed = true;
        }
    }

    void EnsureReadyForNextQuestion()
    {
        if (currentNumber.HasValue && !currentAnswerRevealed)
            throw new PracticeStateException("請先答對或顯示目前題目的答案。");
    }

    void StartNextRoundIfNeeded()
    {
        if (nextIndex < candidateNumbers.Count)
            return;

        Shuffle(candidateNumbers);
        nextIndex = 0;
        round++;
    }

    static void Shuffle(IList<int> numbers)
    {
        for (int index = numbers.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (numbers[index], numbers[swapIndex]) =
                (numbers[swapIndex], numbers[index]);
        }
    }
}

internal sealed record PracticeQuestionPosition(
    int Number,
    int Round,
    int Position,
    int Total);
