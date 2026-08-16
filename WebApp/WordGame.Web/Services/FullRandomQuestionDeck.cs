namespace WordGame.Web.Services;

internal sealed class FullRandomQuestionDeck(
    IReadOnlyList<int> candidateNumbers,
    IRandomSource randomSource)
{
    readonly List<int> candidateNumbers = [.. candidateNumbers];
    int nextIndex = candidateNumbers.Count;
    int round;

    public PracticeQuestionPosition MoveToNextQuestion()
    {
        StartNextRoundIfNeeded();
        int number = candidateNumbers[nextIndex];
        nextIndex++;
        return new PracticeQuestionPosition(
            number,
            round,
            nextIndex,
            candidateNumbers.Count);
    }

    void StartNextRoundIfNeeded()
    {
        if (nextIndex < candidateNumbers.Count)
            return;

        Shuffle();
        nextIndex = 0;
        round++;
    }

    void Shuffle()
    {
        for (int index = candidateNumbers.Count - 1; index > 0; index--)
        {
            int swapIndex = randomSource.NextInt(index + 1);
            (candidateNumbers[index], candidateNumbers[swapIndex]) =
                (candidateNumbers[swapIndex], candidateNumbers[index]);
        }
    }
}
