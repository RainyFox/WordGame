using System.Text.Json.Serialization;
using WordGame.Web.Data;
using WordGame.Web.Endpoints;
using WordGame.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.WebHost.UseUrls("http://127.0.0.1:5276");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOptions<WordGameDatabaseOptions>()
    .BindConfiguration(WordGameDatabaseOptions.SectionName)
    .Validate(options => !options.UseTurso || options.IsTursoConfigured(),
        "Turso requires a database URL (libsql:// or https://) and an auth token.")
    .ValidateOnStart();
builder.Services.AddSingleton<IWordGameDatabasePathResolver, WordGameDatabasePathResolver>();
builder.Services.AddSingleton<SqliteReadOnlyWordGameConnectionFactory>();
builder.Services.AddSingleton<SqliteReadWriteWordGameConnectionFactory>();
builder.Services.AddSingleton<TursoWordGameConnectionFactory>();
builder.Services.AddScoped<WordGameDatabaseSelection>();
builder.Services.AddScoped<IReadOnlyWordGameConnectionFactory>(services =>
    services.GetRequiredService<WordGameDatabaseSelection>().Source == WordGameDatabaseSource.Turso
        ? services.GetRequiredService<TursoWordGameConnectionFactory>()
        : services.GetRequiredService<SqliteReadOnlyWordGameConnectionFactory>());
builder.Services.AddScoped<IReadWriteWordGameConnectionFactory>(services =>
    services.GetRequiredService<WordGameDatabaseSelection>().Source == WordGameDatabaseSource.Turso
        ? services.GetRequiredService<TursoWordGameConnectionFactory>()
        : services.GetRequiredService<SqliteReadWriteWordGameConnectionFactory>());
builder.Services.AddScoped<IVocabularyReadRepository, SqliteVocabularyReadRepository>();
builder.Services.AddScoped<IPracticeVocabularyRepository,
    SqlitePracticeVocabularyRepository>();
builder.Services.AddScoped<IUserProgressRepository, SqliteUserProgressRepository>();
builder.Services.AddSingleton<IRandomSource, SystemRandomSource>();
builder.Services.AddScoped<IMultipleChoiceService, MultipleChoiceService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPracticeSessionService, PracticeSessionService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.Use(DatabaseEndpoints.SelectDatabaseAsync);

app.MapDatabaseEndpoints();
app.MapPracticeEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
