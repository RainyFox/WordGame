using WordGame.Web.Models;
using WordGame.Web.Services;

namespace WordGame.Web.Tests;

public sealed class ReviewCandidateCalculatorTests
{
    static readonly DateTimeOffset Now =
        new(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_SeparatesNewFutureAndUnscheduledCandidates()
    {
        ReviewCandidateProgress[] progress =
        [
            new(1, null, null),
            new(2, Format(Now.AddDays(-1)), Format(Now.AddTicks(1))),
            new(3, Format(Now.AddDays(-1)), null)
        ];

        IReadOnlyList<ReviewCandidate> candidates =
            ReviewCandidateCalculator.Calculate(progress, Now);

        Assert.True(candidates[0].IsNew);
        Assert.Equal(0, candidates[1].SelectionWeight);
        Assert.Equal(1, candidates[2].SelectionWeight);
    }

    [Fact]
    public void Calculate_ChangesFromZeroToDueAtNextReviewBoundary()
    {
        string lastAnswer = Format(Now.AddDays(-1));
        ReviewCandidateProgress[] progress =
        [
            new(1, lastAnswer, Format(Now.AddTicks(1))),
            new(2, lastAnswer, Format(Now))
        ];

        IReadOnlyList<ReviewCandidate> candidates =
            ReviewCandidateCalculator.Calculate(progress, Now);

        Assert.Equal(0, candidates[0].SelectionWeight);
        Assert.Equal(1, candidates[1].SelectionWeight);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    [InlineData(8, 4)]
    public void Calculate_UsesLogarithmicOverdueWeight(
        int elapsedIntervals,
        double expectedWeight)
    {
        DateTimeOffset lastAnswer = Now.AddDays(-elapsedIntervals);
        DateTimeOffset nextReview = lastAnswer.AddDays(1);

        double weight = CalculateWeight(lastAnswer, nextReview);

        Assert.Equal(expectedWeight, weight, precision: 8);
    }

    [Fact]
    public void Calculate_TreatsEquivalentOffsetTimestampAsDue()
    {
        var lastAnswer = new DateTimeOffset(
            2026,
            8,
            15,
            9,
            0,
            0,
            TimeSpan.FromHours(9));
        var nextReview = new DateTimeOffset(
            2026,
            8,
            16,
            9,
            0,
            0,
            TimeSpan.FromHours(9));

        double weight = CalculateWeight(lastAnswer, nextReview);

        Assert.Equal(1, weight);
    }

    [Fact]
    public void Calculate_UsesBaseWeightForInvalidSchedule()
    {
        ReviewCandidateProgress[] progress =
        [
            new(1, Format(Now.AddDays(-1)), "invalid-date"),
            new(2, Format(Now), Format(Now.AddDays(-1)))
        ];

        IReadOnlyList<ReviewCandidate> candidates =
            ReviewCandidateCalculator.Calculate(progress, Now);

        Assert.All(candidates, candidate => Assert.Equal(1, candidate.SelectionWeight));
    }

    static double CalculateWeight(
        DateTimeOffset lastAnswer,
        DateTimeOffset nextReview)
    {
        ReviewCandidateProgress[] progress =
        [new(1, Format(lastAnswer), Format(nextReview))];
        return ReviewCandidateCalculator.Calculate(progress, Now)[0].SelectionWeight;
    }

    static string Format(DateTimeOffset timestamp)
    {
        return timestamp.ToString("O");
    }
}
