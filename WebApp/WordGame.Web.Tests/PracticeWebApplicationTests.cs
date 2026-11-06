using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using WordGame.Web.Models;
using WordGame.Web.Services;
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
        Assert.Equal(4, await GetDailyCountAsync(client));
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
        Assert.Equal(0, await GetDailyCountAsync(client));
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
        Assert.Equal(1, await GetDailyCountAsync(client));
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
        Assert.Equal(1, await GetDailyCountAsync(client));
    }

    [Fact]
    public async Task ProficiencySession_SelectsDueThenNewCandidate()
    {
        using var database = new TemporaryWordGameDatabase();
        var randomSource = new SequenceRandomSource(
            [ProficiencyQuestionSelector.NewCandidateSelectionRate, 0, 0, 0]);
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
        Assert.Equal(1, await GetDailyCountAsync(client));
    }

    [Fact]
    public async Task DailySummary_CountsRepeatsAcrossModesAndDirectionsAndSurvivesServerRestart()
    {
        using var database = new TemporaryWordGameDatabase();
        await using (WebApplicationFactory<Program> factory = CreateFactory(database))
        {
            using HttpClient client = factory.CreateClient();
            byte[] before = database.ComputeHash();
            Assert.Equal(0, await GetDailyCountAsync(client));
            Assert.Equal(before, database.ComputeHash());
            StartPracticeResponse randomSession = await StartSessionAsync(client, 2, 2);
            await RevealAsync(client, randomSession.SessionId);
            await GetNextQuestionAsync(client, randomSession.SessionId);
            await SubmitAnswerAsync(client, randomSession.SessionId, "しゅうとく");
            StartPracticeResponse reviewSession = await StartSessionAsync(
                client, 2, 2, mode: "Proficiency", direction: "CnToJp");
            await SubmitAnswerAsync(client, reviewSession.SessionId, "習得");
            Assert.Equal(3, await GetDailyCountAsync(client));
        }

        await using WebApplicationFactory<Program> restarted = CreateFactory(database);
        using HttpClient restartedClient = restarted.CreateClient();
        Assert.Equal(3, await GetDailyCountAsync(restartedClient));
        DailyPracticeSummary? nextDay = await restartedClient.GetFromJsonAsync<DailyPracticeSummary>(
            "/api/practice/daily-summary?start=2026-08-17T00:00:00Z&end=2026-08-18T00:00:00Z");
        Assert.NotNull(nextDay);
        Assert.Equal(0, nextDay.CompletedQuestions);
    }

    [Theory]
    [InlineData("2026-08-16T00:00:00Z", "2026-08-16T00:00:00Z")]
    [InlineData("2026-08-17T00:00:00Z", "2026-08-16T00:00:00Z")]
    [InlineData("2026-08-16T00:00:00Z", "2026-08-18T00:00:00Z")]
    [InlineData("invalid", "2026-08-17T00:00:00Z")]
    public async Task DailySummary_RejectsInvalidDateRange(string start, string end)
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/practice/daily-summary?start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    static async Task<int> GetDailyCountAsync(HttpClient client)
    {
        DailyPracticeSummary? summary = await client.GetFromJsonAsync<DailyPracticeSummary>(
            "/api/practice/daily-summary?start=2026-08-16T00:00:00Z&end=2026-08-17T00:00:00Z");
        Assert.NotNull(summary);
        return summary.CompletedQuestions;
    }

    [Theory]
    [InlineData("FullRandom", "JpToCn", "ととのう", 2, 4, 2)]
    [InlineData("Proficiency", "JpToCn", "ととのう", 2, 4, 2)]
    [InlineData("FullRandom", "CnToJp", "整う", 0, 0, 1)]
    [InlineData("Proficiency", "CnToJp", "整う", 0, 0, 1)]
    public async Task MarkWrong_UpdatesProgressAndExistingHistoryAcrossModesAndDirections(
        string mode, string direction, string answer,
        int expectedProficiency, int expectedCorrect, int expectedWrong)
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 1, 1, mode, direction);
        PracticeAnswerResponse correct = await SubmitAnswerAsync(client, session.SessionId, answer);

        HttpResponseMessage response = await PostMarkWrongAsync(client, session.SessionId, correct.AnswerId!.Value);
        response.EnsureSuccessStatusCode();
        PracticeAnswerResponse? corrected = await response.Content.ReadFromJsonAsync<PracticeAnswerResponse>(JsonOptions);

        Assert.NotNull(corrected);
        Assert.Equal(PracticeOutcome.Wrong, corrected.RecordedOutcome);
        Assert.NotNull(corrected.Progress);
        Assert.Equal(expectedProficiency, corrected.Progress.Proficiency);
        Assert.Equal(expectedCorrect, corrected.Progress.TotalCorrect);
        Assert.Equal(expectedWrong, corrected.Progress.TotalWrong);
        Assert.Equal(correct.Progress!.LastAnswer, corrected.Progress.LastAnswer);
        Assert.Equal(AnsweredAt.AddDays(1), corrected.Progress.NextReview);
        Assert.Equal(correct.AnswerId, corrected.AnswerId);
        Assert.Equal(0, ReadHistoryResult(database, corrected.AnswerId!.Value));
        Assert.Equal(1, await GetDailyCountAsync(client));
        if (direction == "CnToJp")
            Assert.Equal(3, database.ReadProgress(1, PracticeDirection.JpToCn)!.Proficiency);

        await GetNextQuestionAsync(client, session.SessionId);
    }

    [Fact]
    public async Task MarkWrong_RejectsUnansweredAndAlreadyWrongQuestions()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 2, 2);
        Assert.Equal(HttpStatusCode.Conflict, (await PostMarkWrongAsync(client, session.SessionId, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostMarkWrongAsync(client, session.SessionId, 0)).StatusCode);
        await SubmitAnswerAsync(client, session.SessionId, "wrong");
        PracticeAnswerResponse settled = await SubmitAnswerAsync(client, session.SessionId, "しゅうとく");
        Assert.Equal(HttpStatusCode.Conflict,
            (await PostMarkWrongAsync(client, session.SessionId, settled.AnswerId!.Value)).StatusCode);
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Equal(1, await GetDailyCountAsync(client));
    }

    [Fact]
    public async Task MarkWrong_RejectsStaleAnswerIdWhenSameWordRepeats()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 2, 2);
        PracticeAnswerResponse first = await SubmitAnswerAsync(client, session.SessionId, "しゅうとく");
        await GetNextQuestionAsync(client, session.SessionId);
        PracticeAnswerResponse second = await SubmitAnswerAsync(client, session.SessionId, "しゅうとく");

        Assert.Equal(HttpStatusCode.Conflict,
            (await PostMarkWrongAsync(client, session.SessionId, first.AnswerId!.Value)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await PostMarkWrongAsync(client, session.SessionId, second.AnswerId!.Value)).StatusCode);
        Assert.Equal(1, ReadHistoryResult(database, first.AnswerId.Value));
        Assert.Equal(0, ReadHistoryResult(database, second.AnswerId.Value));
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalCorrect);
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Equal(2, await GetDailyCountAsync(client));
    }

    [Fact]
    public async Task MarkWrong_ConcurrentRequestsCorrectOnlyOnce()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 2, 2);
        PracticeAnswerResponse correct = await SubmitAnswerAsync(client, session.SessionId, "しゅうとく");

        HttpResponseMessage[] responses = await Task.WhenAll(
            PostMarkWrongAsync(client, session.SessionId, correct.AnswerId!.Value),
            PostMarkWrongAsync(client, session.SessionId, correct.AnswerId.Value));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalCorrect);
        Assert.Equal(1, database.ReadProgress(2, PracticeDirection.JpToCn)!.TotalWrong);
        Assert.Equal(1, await GetDailyCountAsync(client));
    }

    [Fact]
    public async Task MarkWrong_DoesNotOverwriteNewerProgressFromAnotherSession()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse firstSession = await StartSessionAsync(client, 2, 2);
        PracticeAnswerResponse first = await SubmitAnswerAsync(client, firstSession.SessionId, "しゅうとく");
        StartPracticeResponse secondSession = await StartSessionAsync(client, 2, 2);
        await SubmitAnswerAsync(client, secondSession.SessionId, "しゅうとく");
        StoredProgressRow? before = database.ReadProgress(2, PracticeDirection.JpToCn);

        Assert.Equal(HttpStatusCode.Conflict,
            (await PostMarkWrongAsync(client, firstSession.SessionId, first.AnswerId!.Value)).StatusCode);
        Assert.Equal(before, database.ReadProgress(2, PracticeDirection.JpToCn));
        Assert.Equal(1, ReadHistoryResult(database, first.AnswerId.Value));
        Assert.Equal(2, await GetDailyCountAsync(client));
    }

    [Fact]
    public async Task MarkWrong_WriteFailureRollsBackBothTablesAndAllowsRetry()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory = CreateFactory(database);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 2, 2);
        PracticeAnswerResponse correct = await SubmitAnswerAsync(client, session.SessionId, "しゅうとく");
        StoredProgressRow? before = database.ReadProgress(2, PracticeDirection.JpToCn);
        using var connection = new SqliteConnection($"Data Source={database.DatabasePath};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER FailCorrection BEFORE UPDATE ON UserProgress
            WHEN NEW.TotalWrong > OLD.TotalWrong
            BEGIN SELECT RAISE(ABORT, 'test correction failure'); END;
            """;
        command.ExecuteNonQuery();

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await PostMarkWrongAsync(client, session.SessionId, correct.AnswerId!.Value)).StatusCode);
        Assert.Equal(before, database.ReadProgress(2, PracticeDirection.JpToCn));
        Assert.Equal(1, ReadHistoryResult(database, correct.AnswerId.Value));
        command.CommandText = "DROP TRIGGER FailCorrection";
        command.ExecuteNonQuery();

        Assert.Equal(HttpStatusCode.OK,
            (await PostMarkWrongAsync(client, session.SessionId, correct.AnswerId.Value)).StatusCode);
        Assert.Equal(0, ReadHistoryResult(database, correct.AnswerId.Value));
        Assert.Equal(1, await GetDailyCountAsync(client));
    }

    static Task<HttpResponseMessage> PostMarkWrongAsync(HttpClient client, Guid sessionId, long answerId)
    {
        return client.PostAsJsonAsync($"/api/practice/sessions/{sessionId}/mark-wrong", new { answerId });
    }

    static int ReadHistoryResult(TemporaryWordGameDatabase database, long answerId)
    {
        using var connection = new SqliteConnection($"Data Source={database.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT IsCorrect FROM PracticeHistory WHERE Id = $id";
        command.Parameters.AddWithValue("$id", answerId);
        return Convert.ToInt32(command.ExecuteScalar());
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
