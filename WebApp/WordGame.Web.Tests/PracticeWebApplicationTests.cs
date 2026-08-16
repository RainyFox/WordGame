using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class PracticeWebApplicationTests
{
    [Fact]
    public async Task FullRandomSession_DoesNotRepeatWithinRoundOrWriteDatabase()
    {
        using var database = new TemporaryWordGameDatabase();
        byte[] hashBeforePractice = database.ComputeHash();
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(database.DatabasePath);
        using HttpClient client = factory.CreateClient();

        StartPracticeResponse session = await StartSessionAsync(client, 1, 4);
        var selectedNumbers = new HashSet<int>();
        PracticeQuestionResponse question = session.Question;

        for (int position = 1; position <= 4; position++)
        {
            Assert.Equal(1, question.Round);
            Assert.Equal(position, question.Position);
            Assert.Equal(4, question.Total);
            Assert.True(selectedNumbers.Add(question.Number));

            PracticeAnswerResponse reveal = await RevealAsync(client, session.SessionId);
            Assert.False(reveal.IsCorrect);
            Assert.NotNull(reveal.Reveal);

            if (position < 4)
                question = await GetNextQuestionAsync(client, session.SessionId);
        }

        PracticeQuestionResponse nextRound = await GetNextQuestionAsync(
            client,
            session.SessionId);
        Assert.Equal(2, nextRound.Round);
        Assert.Equal(1, nextRound.Position);
        Assert.Equal(hashBeforePractice, database.ComputeHash());
    }

    [Fact]
    public async Task AnswerFlow_AllowsRetryThenRevealsCorrectAnswer()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(database.DatabasePath);
        using HttpClient client = factory.CreateClient();
        StartPracticeResponse session = await StartSessionAsync(client, 1, 1);

        PracticeAnswerResponse wrong = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "錯誤答案");
        HttpResponseMessage prematureNext = await client.PostAsync(
            $"/api/practice/sessions/{session.SessionId}/next",
            null,
            CancellationToken.None);
        PracticeAnswerResponse correct = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "ととのう");

        Assert.False(wrong.IsCorrect);
        Assert.Null(wrong.Reveal);
        Assert.Equal(HttpStatusCode.Conflict, prematureNext.StatusCode);
        Assert.True(correct.IsCorrect);
        Assert.NotNull(correct.Reveal);
        Assert.Equal("ととのう", correct.Reveal.Answer);
        Assert.Equal("整理", correct.Reveal.Translation);
        Assert.Equal("部屋が整う", correct.Reveal.Example);
    }

    [Fact]
    public async Task ChineseToJapaneseSession_UsesWordAsAnswer()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(database.DatabasePath);
        using HttpClient client = factory.CreateClient();

        StartPracticeResponse session = await StartSessionAsync(
            client,
            1,
            1,
            "CnToJp");
        PracticeAnswerResponse correct = await SubmitAnswerAsync(
            client,
            session.SessionId,
            "整う");

        Assert.Equal("整理", session.Question.Prompt);
        Assert.True(correct.IsCorrect);
        Assert.NotNull(correct.Reveal);
        Assert.Equal("整う", correct.Reveal.Answer);
        Assert.Equal("ととのう", correct.Reveal.Translation);
    }

    [Fact]
    public async Task StartSession_RejectsInvalidOrEmptySelection()
    {
        using var database = new TemporaryWordGameDatabase();
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(database.DatabasePath);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage invalidRange = await PostStartRequestAsync(
            client,
            4,
            1,
            null,
            "JpToCn");
        HttpResponseMessage emptyType = await PostStartRequestAsync(
            client,
            1,
            2,
            "文法",
            "JpToCn");

        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyType.StatusCode);
    }

    static async Task<StartPracticeResponse> StartSessionAsync(
        HttpClient client,
        int minNumber,
        int maxNumber,
        string direction = "JpToCn")
    {
        HttpResponseMessage response = await PostStartRequestAsync(
            client,
            minNumber,
            maxNumber,
            null,
            direction);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartPracticeResponse>(
            CancellationToken.None))!;
    }

    static Task<HttpResponseMessage> PostStartRequestAsync(
        HttpClient client,
        int minNumber,
        int maxNumber,
        string? type,
        string direction)
    {
        return client.PostAsJsonAsync(
            "/api/practice/sessions",
            new { minNumber, maxNumber, type, direction },
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
            CancellationToken.None))!;
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
            CancellationToken.None))!;
    }
}
