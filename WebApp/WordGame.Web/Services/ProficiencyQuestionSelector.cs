using WordGame.Web.Models;

namespace WordGame.Web.Services;

public static class ProficiencyQuestionSelector
{
    public const double NewCandidateSelectionRate = 0.15;

    public static ReviewCandidate Select(
        IReadOnlyList<ReviewCandidate> candidates,
        double poolRandomValue,
        double candidateRandomValue)
    {
        if (candidates.Count == 0)
            throw new PracticeValidationException("指定範圍與類型內沒有單字。");

        ReviewCandidate[] newCandidates = candidates.Where(x => x.IsNew).ToArray();
        ReviewCandidate[] dueCandidates = candidates
            .Where(x => !x.IsNew && IsValidWeight(x.SelectionWeight))
            .ToArray();

        if (ShouldSelectNewCandidate(newCandidates, dueCandidates, poolRandomValue))
            return SelectUniformly(newCandidates, candidateRandomValue);
        if (dueCandidates.Length > 0)
            return SelectByWeight(dueCandidates, candidateRandomValue);
        return SelectFallback(candidates, candidateRandomValue);
    }

    static bool ShouldSelectNewCandidate(
        IReadOnlyCollection<ReviewCandidate> newCandidates,
        IReadOnlyCollection<ReviewCandidate> dueCandidates,
        double randomValue)
    {
        if (newCandidates.Count == 0)
            return false;
        return dueCandidates.Count == 0
            || NormalizeRandomValue(randomValue) < NewCandidateSelectionRate;
    }

    static ReviewCandidate SelectByWeight(
        IReadOnlyList<ReviewCandidate> candidates,
        double randomValue)
    {
        double totalWeight = candidates.Sum(x => x.SelectionWeight);
        double target = NormalizeRandomValue(randomValue) * totalWeight;
        double cumulativeWeight = 0;

        foreach (ReviewCandidate candidate in candidates)
        {
            cumulativeWeight += candidate.SelectionWeight;
            if (target < cumulativeWeight)
                return candidate;
        }

        return candidates[^1];
    }

    static ReviewCandidate SelectFallback(
        IReadOnlyList<ReviewCandidate> candidates,
        double randomValue)
    {
        ReviewCandidate? closestReview = candidates
            .Where(x => x.NextReview.HasValue)
            .MinBy(x => x.NextReview);
        return closestReview ?? SelectUniformly(candidates, randomValue);
    }

    static ReviewCandidate SelectUniformly(
        IReadOnlyList<ReviewCandidate> candidates,
        double randomValue)
    {
        int index = Math.Min(
            (int)(NormalizeRandomValue(randomValue) * candidates.Count),
            candidates.Count - 1);
        return candidates[index];
    }

    static bool IsValidWeight(double weight)
    {
        return weight > 0 && !double.IsNaN(weight) && !double.IsInfinity(weight);
    }

    static double NormalizeRandomValue(double value)
    {
        return double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
    }
}
