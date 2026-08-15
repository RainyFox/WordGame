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

Only the directory structure has been prepared. The machine currently has a
.NET runtime but no .NET SDK, so the ASP.NET Core project will be scaffolded
after a compatible SDK is installed.
