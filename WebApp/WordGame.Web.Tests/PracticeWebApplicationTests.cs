using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class PracticeWebApplicationTests
{
    static readonly DateTimeOffset AnsweredAt =
        new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task FullRandomSession_DoesNotRepeatAndSettlesEveryQuestionOnce()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 1, 4);
        var selectedNumbers = new HashSet<int>();
        PracticeQuestionResponse question = session.Question;

        for (int position = 1; position <= 4; position++)
        {
            Assert.Equal(PracticeMode.FullRandom, question.Mode);
            Assert.Equal(1, question.Round);
            Assert.Equal(position, question.Position);
            Assert.Equal(4, question.Total);
            Assert.True(selectedNumbers.Add(question.Number));

            PracticeAnswerResponse reveal = await RevealAsync(client, session.SessionId);
            Assert.False(reveal.IsCorrect);
            Assert.Equal(PracticeOutcome.Wrong, reveal.RecordedOutcome);
            Assert.NotNull(reveal.Progress);

            if (position < 4)
                question = await GetNextQuestionAsync(client, session.SessionId);
        }

        PracticeQuestionResponse nextRound = await GetNextQuestionAsync(
            client,
            session.SessionId);
        Assert.Equal(2, nextRound.Round);
        Assert.Equal(1, nextRound.Position);
        Assert.Equal(2, database.ReadProgress(1, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Equal(1, database.ReadProgress(3, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Equal(1, database.ReadProgress(4, PracticeDirection.JpToCn)!.TotalWrong);
    }

    [Fact]
    public async Task RetryThenCorrect_RecordsOnlyOneWrongResult()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 2, 2);

        PracticeAnswerResponse firstWrong = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "錯誤答案");
        PracticeAnswerResponse secondWrong = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "仍然錯誤");
        HttpResponseMessage prematureNext = await client.PostAsync(
            $"/api/practice/sessions/{session.SessionId}/next",
            null,
            CancellationToken.None);
        StoredProgressRow? beforeSettlement = database.ReadProgress(
            2,
            PracticeDirection.JpToCn);
        PracticeAnswerResponse correct = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "しゅうとく");

        Assert.Null(firstWrong.RecordedOutcome);
        Assert.Null(secondWrong.RecordedOutcome);
        Assert.Null(beforeSettlement);
        Assert.Equal(HttpStatusCode.Conflict, prematureNext.StatusCode);
        Assert.True(correct.IsCorrect);
        Assert.Equal(PracticeOutcome.Wrong, correct.RecordedOutcome);
        Assert.NotNull(correct.Progress);
        Assert.Equal(0, correct.Progress.TotalCorrect);
        Assert.Equal(1, correct.Progress.TotalWrong);
        Assert.Equal(AnsweredAt.AddDays(1), correct.Progress.NextReview);
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalWrong);
    }

    [Fact]
    public async Task FirstTryCorrect_RecordsCorrectForSelectedDirection()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(
            client,
            1,
            1,
            direction: "CnToJp");

        PracticeAnswerResponse correct = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "整う");

        Assert.Equal("整理", session.Question.Prompt);
        Assert.Equal(4, session.Question.Choices.Count);
        Assert.Equal(4, session.Question.Choices.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("整う", session.Question.Choices);
        Assert.True(correct.IsCorrect);
        Assert.Equal(PracticeOutcome.Correct, correct.RecordedOutcome);
        Assert.NotNull(correct.Progress);
        Assert.Equal(1, correct.Progress.Proficiency);
        Assert.Equal(AnsweredAt.AddDays(2), correct.Progress.NextReview);
        Assert.Equal(3, database.ReadProgress(1, PracticeDirection.JpToCn)!.Proficiency);
        Assert.Equal(1, database.ReadProgress(1, PracticeDirection.CnToJp)!.Proficiency);
    }

    [Fact]
    public async Task EndSession_SettlesPriorWrongButIgnoresUntouchedQuestion()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse attemptedSession = await StartSessionAsync(client, 3, 3);
        StartPracticeResponse untouchedSession = await StartSessionAsync(client, 4, 4);

        await SubmitAnswerAsync(client, attemptedSession.SessionId, "錯誤答案");
        HttpResponseMessage attemptedEnd = await client.DeleteAsync(
            $"/api/practice/sessions/{attemptedSession.SessionId}",
            CancellationToken.None);
        HttpResponseMessage untouchedEnd = await client.DeleteAsync(
            $"/api/practice/sessions/{untouchedSession.SessionId}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, attemptedEnd.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, untouchedEnd.StatusCode);
        Assert.Equal(1, database.ReadProgress(3, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Null(database.ReadProgress(4, PracticeDirection.JpToCn));
    }

    [Fact]
    public async Task ProficiencySession_SelectsDueThenNewCandidate()
    {
        using var database = new TemporaryWordGameDatabase();
        var randomSource = new SequenceRandomSource([0.5, 0, 0, 0]);
        await using WebApplicationFactory<Program> factory = CreateFactory(
            database,
            randomSource);
        using HttpClient client = factory.CreateClient();

        StartPracticeResponse session = await StartSessionAsync(
            client,
            1,
            4,
            mode: "Proficiency");
        await RevealAsync(client, session.SessionId);
        PracticeQuestionResponse next = await GetNextQuestionAsync(
            client,
            session.SessionId);

        Assert.Equal(PracticeMode.Proficiency, session.Question.Mode);
        Assert.Equal(1, session.Question.Number);
        Assert.Equal(4, session.Question.Total);
        Assert.Equal(PracticeMode.Proficiency, next.Mode);
        Assert.Equal(2, next.Number);
    }

    [Fact]
    public async Task StartSession_RejectsInvalidOrEmptySelection()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage invalidRange = await PostStartRequestAsync(
            client,
            4,
            1,
            null,
            "FullRandom",
            "JpToCn");
        HttpResponseMessage emptyType = await PostStartRequestAsync(
            client,
            1,
            2,
            "文法",
            "FullRandom",
            "JpToCn");

        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyType.StatusCode);
    }

    [Fact]
    public async Task ConcurrentCorrectSubmissions_RecordProgressOnlyOnce()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 2, 2);

        Task<HttpResponseMessage> firstSubmission = PostAnswerAsync(
            client,
            session.SessionId,
            "しゅうとく");
        Task<HttpResponseMessage> secondSubmission = PostAnswerAsync(
            client,
            session.SessionId,
            "しゅうとく");
        HttpResponseMessage[] responses = await Task.WhenAll(
            firstSubmission,
            secondSubmission);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalCorrect);
    }

    static WebApplicationFactory<Program> CreateFactory(
        TemporaryWordGameDatabase database,
        SequenceRandomSource? randomSource = null)
    {
        return WordGameWebApplicationFactory.Create(
            database.DatabasePath,
            new FixedTimeProvider(AnsweredAt),
            randomSource ?? new SequenceRandomSource());
    }

    static async Task<StartPracticeResponse> StartSessionAsync(
        HttpClient client,
        int minNumber,
        int maxNumber,
        string mode = "FullRandom",
        string direction = "JpToCn")
    {
        HttpResponseMessage response = await PostStartRequestAsync(
            client,
            minNumber,
            maxNumber,
            null,
            mode,
            direction);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartPracticeResponse>(
            JsonOptions,
            CancellationToken.None))!;
    }

    static Task<HttpResponseMessage> PostStartRequestAsync(
        HttpClient client,
        int minNumber,
        int maxNumber,
        string? type,
        string mode,
        string direction)
    {
        return client.PostAsJsonAsync(
            "/api/practice/sessions",
            new { minNumber, maxNumber, type, mode, direction },
            CancellationToken.None);
    }

    static async Task<PracticeAnswerResponse> SubmitAnswerAsync(
        HttpClient client,
        Guid sessionId,
        string answer)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/practice/sessions/{sessionId}/answer",
            new { answer },
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PracticeAnswerResponse>(
            JsonOptions,
            CancellationToken.None))!;
    }

    static Task<HttpResponseMessage> PostAnswerAsync(
        HttpClient client,
        Guid sessionId,
        string answer)
    {
        return client.PostAsJsonAsync(
            $"/api/practice/sessions/{sessionId}/answer",
            new { answer },
            CancellationToken.None);
    }

    static async Task<PracticeAnswerResponse> RevealAsync(
        HttpClient client,
        Guid sessionId)
    {
        HttpResponseMessage response = await client.PostAsync(
            $"/api/practice/sessions/{sessionId}/reveal",
            null,
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PracticeAnswerResponse>(
            JsonOptions,
            CancellationToken.None))!;
    }

    static async Task<PracticeQuestionResponse> GetNextQuestionAsync(
        HttpClient client,
        Guid sessionId)
    {
        HttpResponseMessage response = await client.PostAsync(
            $"/api/practice/sessions/{sessionId}/next",
            null,
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PracticeQuestionResponse>(
            JsonOptions,
            CancellationToken.None))!;
    }

    static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
