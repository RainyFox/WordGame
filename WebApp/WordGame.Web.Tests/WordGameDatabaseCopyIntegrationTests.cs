using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class WordGameDatabaseCopyIntegrationTests
{
    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task FullPracticeFlow_WritesOnlyToRealDatabaseCopy()
    {
        using var database = new CopiedWordGameDatabase();
        byte[] sourceHash = database.ComputeSourceHash();
        IntegrationCandidate candidate = ReadIntegrationCandidate(database);
        HashSet<string> sameTypeAnswers = ReadSameTypeAnswers(database, candidate.Type);
        int correctBefore = ReadTotalCorrect(database, candidate.Number);
        await using WebApplicationFactory<Program> factory =
            WordGameWebApplicationFactory.Create(
                database.DatabasePath,
                new FixedTimeProvider(
                    new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero)),
                new SequenceRandomSource());
        using HttpClient client = factory.CreateClient();

        StartPracticeResponse session = await StartSessionAsync(client, candidate);
        PracticeAnswerResponse result = await SubmitCorrectAnswerAsync(
            client,
            session.SessionId,
            candidate.Answer);

        Assert.Equal(4, session.Question.Choices.Count);
        Assert.Equal(4, session.Question.Choices.Distinct(StringComparer.Ordinal).Count());
        Assert.All(session.Question.Choices, answer => Assert.Contains(answer, sameTypeAnswers));
        Assert.Equal(PracticeOutcome.Correct, result.RecordedOutcome);
        Assert.Equal(correctBefore + 1, ReadTotalCorrect(database, candidate.Number));
        Assert.Equal(sourceHash, database.ComputeSourceHash());
    }

    static async Task<StartPracticeResponse> StartSessionAsync(
        HttpClient client,
        IntegrationCandidate candidate)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/practice/sessions",
            new
            {
                minNumber = candidate.Number,
                maxNumber = candidate.Number,
                type = candidate.Type,
                mode = "FullRandom",
                direction = "JpToCn"
            },
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartPracticeResponse>(
            JsonOptions,
            CancellationToken.None))!;
    }

    static async Task<PracticeAnswerResponse> SubmitCorrectAnswerAsync(
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

    static IntegrationCandidate ReadIntegrationCandidate(
        CopiedWordGameDatabase database)
    {
        using SqliteConnection connection = database.OpenCopyReadOnly();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT V.番号, TRIM(V.タイプ), V.かな
            FROM Vocabulary AS V
            WHERE TRIM(COALESCE(V.かな, '')) <> ''
              AND TRIM(COALESCE(V.タイプ, '')) IN (
                  SELECT TRIM(COALESCE(タイプ, ''))
                  FROM Vocabulary
                  WHERE TRIM(COALESCE(かな, '')) <> ''
                  GROUP BY TRIM(COALESCE(タイプ, ''))
                  HAVING COUNT(DISTINCT かな) >= 4
              )
            ORDER BY V.番号
            LIMIT 1
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read(), "正式資料庫中找不到可建立四選一的單字類型。");
        return new IntegrationCandidate(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    static HashSet<string> ReadSameTypeAnswers(
        CopiedWordGameDatabase database,
        string type)
    {
        using SqliteConnection connection = database.OpenCopyReadOnly();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT かな
            FROM Vocabulary
            WHERE TRIM(COALESCE(タイプ, '')) = $type
              AND TRIM(COALESCE(かな, '')) <> ''
            """;
        command.Parameters.AddWithValue("$type", type);
        using SqliteDataReader reader = command.ExecuteReader();
        var answers = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
            answers.Add(reader.GetString(0));
        return answers;
    }

    static int ReadTotalCorrect(
        CopiedWordGameDatabase database,
        int number)
    {
        using SqliteConnection connection = database.OpenCopyReadOnly();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(TotalCorrect, 0)
            FROM UserProgress
            WHERE 番号 = $number AND Mode = 'JpToCn'
            """;
        command.Parameters.AddWithValue("$number", number);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    sealed record IntegrationCandidate(
        int Number,
        string Type,
        string Answer);
}
