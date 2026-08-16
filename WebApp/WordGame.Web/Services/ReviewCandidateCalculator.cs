using System.Globalization;
using WordGame.Web.Models;

namespace WordGame.Web.Services;

public static class ReviewCandidateCalculator
{
    public static IReadOnlyList<ReviewCandidate> Calculate(
        IReadOnlyList<ReviewCandidateProgress> progressRecords,
        DateTimeOffset now)
    {
        return progressRecords
            .Select(progress => Calculate(progress, now))
            .ToArray();
    }

    static ReviewCandidate Calculate(
        ReviewCandidateProgress progress,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(progress.LastAnswer))
            return new ReviewCandidate(progress.Number, true, null, 1);

        DateTimeOffset? lastAnswer = ParseTimestamp(progress.LastAnswer);
        DateTimeOffset? nextReview = ParseTimestamp(progress.NextReview);
        double weight = CalculateSelectionWeight(lastAnswer, nextReview, now);
        return new ReviewCandidate(progress.Number, false, nextReview, weight);
    }

    static double CalculateSelectionWeight(
        DateTimeOffset? lastAnswer,
        DateTimeOffset? nextReview,
        DateTimeOffset now)
    {
        if (!nextReview.HasValue || !lastAnswer.HasValue)
            return 1;
        if (nextReview.Value > now)
            return 0;
        if (nextReview.Value <= lastAnswer.Value)
            return 1;

        double elapsedTicks = (now - lastAnswer.Value).Ticks;
        double scheduledTicks = (nextReview.Value - lastAnswer.Value).Ticks;
        double overdueRatio = Math.Max(1, elapsedTicks / scheduledTicks);
        return 1 + Math.Log2(overdueRatio);
    }

    static DateTimeOffset? ParseTimestamp(string? timestamp)
    {
        return DateTimeOffset.TryParse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset parsed)
            ? parsed
            : null;
    }
}
