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

Phase 1 provides a read-only ASP.NET Core application that:

- binds to `http://127.0.0.1:5276`;
- resolves the project-root `WordGame.db`;
- reports database health through `/api/health`;
- reports vocabulary count and types through `/api/vocabulary/summary`;
- serves a browser status page from `WordGame.Web/wwwroot`.

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
