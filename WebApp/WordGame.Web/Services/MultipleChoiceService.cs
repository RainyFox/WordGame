using WordGame.Web.Data;
using WordGame.Web.Models;

namespace WordGame.Web.Services;

public sealed class MultipleChoiceService(
    IPracticeVocabularyRepository vocabularyRepository,
    IRandomSource randomSource) : IMultipleChoiceService
{
    const int ChoiceCount = 4;

    public async Task<IReadOnlyList<string>> CreateChoicesAsync(
        VocabularyEntry question,
        PracticeDirection direction,
        CancellationToken cancellationToken)
    {
        string correctAnswer = GetCorrectAnswer(question, direction);
        IReadOnlyList<string> availableDistractors =
            await vocabularyRepository.GetDistractorAnswersAsync(
                question,
                direction,
                cancellationToken);
        List<string> distractors = availableDistractors
            .Where(answer => !string.Equals(
                answer,
                correctAnswer,
                StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        EnsureEnoughDistractors(distractors.Count);

        Shuffle(distractors);
        var choices = new List<string>(ChoiceCount) { correctAnswer };
        choices.AddRange(distractors.Take(ChoiceCount - 1));
        Shuffle(choices);
        return choices;
    }

    void Shuffle(IList<string> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int otherIndex = randomSource.NextInt(index + 1);
            (values[index], values[otherIndex]) = (values[otherIndex], values[index]);
        }
    }

    static void EnsureEnoughDistractors(int distractorCount)
    {
        if (distractorCount < ChoiceCount - 1)
        {
            throw new PracticeValidationException(
                "單字庫中沒有足夠的不同答案建立四選一選項。");
        }
    }

    static string GetCorrectAnswer(
        VocabularyEntry question,
        PracticeDirection direction)
    {
        return direction == PracticeDirection.JpToCn
            ? question.Kana
            : question.Word;
    }
}
