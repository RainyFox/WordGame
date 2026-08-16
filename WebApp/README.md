# WordGame Web

This directory will contain the browser-based vocabulary practice application.
It is kept outside `Assets` so Unity does not import or compile the web project.

## Database boundary

- Read vocabulary from `../WordGame.db`.
- Do not modify the `Vocabulary` table from the practice application.
- Read and update only `UserProgress` while practicing.
- Do not run the Unity practice scene and the web practice server at the same time.

## Planned structure

- `WordGame.Web/Data`: SQLite access and database queries.
- `WordGame.Web/Models`: request, response, vocabulary, and progress models.
- `WordGame.Web/Services`: session, question selection, and review scheduling logic.
- `WordGame.Web/wwwroot`: browser HTML, CSS, and JavaScript.
- `WordGame.Web.Tests`: unit and database integration tests.

## Current status

Phase 4 provides an ASP.NET Core practice application that:

- binds to `http://127.0.0.1:5276`;
- resolves the project-root `WordGame.db`;
- reports database health through `/api/health`;
- reports vocabulary count and types through `/api/vocabulary/summary`;
- filters practice words by actual `番号` range and `タイプ`;
- supports Japanese-to-kana and Chinese-to-Japanese questions;
- shuffles every matching word without repeats inside a round;
- selects proficiency-review questions with the same new/due ratio, overdue
  weight, and closest-review fallback as the Unity application;
- supports retrying a wrong answer or revealing an unknown answer;
- records `UserProgress` once per completed question;
- treats a correct answer after any wrong attempt as one wrong result;
- updates proficiency, answer totals, `LastAnswer`, and `NextReview`;
- serves the complete browser practice flow from `WordGame.Web/wwwroot`.

Practice sessions are kept in server memory and expire after eight hours of
inactivity. Leaving after a wrong attempt records one wrong result; leaving an
untouched question does not change progress. `Vocabulary` always remains
read-only.

### Proficiency review selection

- New and due questions are kept in separate pools.
- When both pools contain candidates, a question comes from the new pool 15%
  of the time and from the due pool 85% of the time.
- If either pool is empty, the other pool is used automatically.
- Future questions have zero selection weight.
- Due-question weight is `1 + log2(elapsed interval / scheduled interval)`.
- If neither pool contains a candidate, the question with the closest
  `NextReview` is selected.
- Time and randomness are injectable so boundary and distribution tests remain
  deterministic.

## Development

Requirements: .NET 10 SDK.

Run the application:

```powershell
dotnet run --project WebApp/WordGame.Web/WordGame.Web.csproj
```

Run all tests:

```powershell
dotnet test WebApp/WordGame.Web.sln
```
