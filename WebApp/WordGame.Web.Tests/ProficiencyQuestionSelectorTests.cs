using WordGame.Web.Models;
using WordGame.Web.Services;

namespace WordGame.Web.Tests;

public sealed class ProficiencyQuestionSelectorTests
{
    static readonly DateTimeOffset Now =
        new(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Select_UsesNewPoolBelowFifteenPercentBoundary()
    {
        ReviewCandidate[] candidates = CreateNewAndDueCandidates();

        ReviewCandidate selectedNew = Select(candidates, 0.149, 0);
        ReviewCandidate selectedDue = Select(candidates, 0.15, 0);

        Assert.True(selectedNew.IsNew);
        Assert.False(selectedDue.IsNew);
    }

    [Fact]
    public void Select_UsesDuePoolWhenNewPoolIsEmpty()
    {
        ReviewCandidate[] candidates =
        [new(2, false, Now, 1)];

        ReviewCandidate selected = Select(candidates, 0, 0);

        Assert.Equal(2, selected.Number);
    }

    [Fact]
    public void Select_UsesNewPoolWhenDuePoolIsEmpty()
    {
        ReviewCandidate[] candidates =
        [
            new(1, true, null, 1),
            new(2, false, Now.AddDays(1), 0)
        ];

        ReviewCandidate selected = Select(candidates, 0.99, 0);

        Assert.Equal(1, selected.Number);
    }

    [Fact]
    public void Select_DistributesNewAndDuePoolsAtFifteenToEightyFive()
    {
        ReviewCandidate[] candidates = CreateNewAndDueCandidates();
        const int sampleCount = 10_000;

        int newSelections = CountSelections(
            candidates,
            sampleCount,
            candidate => candidate.IsNew);

        Assert.Equal(1_500, newSelections);
        Assert.Equal(8_500, sampleCount - newSelections);
    }

    [Fact]
    public void Select_DistributesDueCandidatesAccordingToTheirWeights()
    {
        ReviewCandidate[] candidates =
        [
            new(2, false, Now, 1),
            new(3, false, Now, 3)
        ];
        const int sampleCount = 4_000;

        int firstCandidateSelections = CountSelections(
            candidates,
            sampleCount,
            candidate => candidate.Number == 2);

        Assert.Equal(1_000, firstCandidateSelections);
        Assert.Equal(3_000, sampleCount - firstCandidateSelections);
    }

    [Fact]
    public void Select_WhenBothPoolsAreEmpty_UsesClosestReview()
    {
        ReviewCandidate[] candidates =
        [
            new(1, false, Now.AddDays(5), 0),
            new(2, false, Now.AddDays(2), 0),
            new(3, false, Now.AddDays(8), 0)
        ];

        ReviewCandidate selected = Select(candidates, 0.5, 0.5);

        Assert.Equal(2, selected.Number);
    }

    static ReviewCandidate[] CreateNewAndDueCandidates()
    {
        return
        [
            new(1, true, null, 1),
            new(2, false, Now, 1)
        ];
    }

    static ReviewCandidate Select(
        IReadOnlyList<ReviewCandidate> candidates,
        double poolRandomValue,
        double candidateRandomValue)
    {
        return ProficiencyQuestionSelector.Select(
            candidates,
            poolRandomValue,
            candidateRandomValue);
    }

    static int CountSelections(
        IReadOnlyList<ReviewCandidate> candidates,
        int sampleCount,
        Func<ReviewCandidate, bool> predicate)
    {
        return Enumerable.Range(0, sampleCount)
            .Select(index => Select(
                candidates,
                CreateEvenlyDistributedValue(index, sampleCount),
                CreateEvenlyDistributedValue(index, sampleCount)))
            .Count(predicate);
    }

    static double CreateEvenlyDistributedValue(int index, int sampleCount)
    {
        return (index + 0.5) / sampleCount;
    }
}
