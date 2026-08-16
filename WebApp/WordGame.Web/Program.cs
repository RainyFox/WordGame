using System.Text.Json.Serialization;
using WordGame.Web.Data;
using WordGame.Web.Endpoints;
using WordGame.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5276");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<WordGameDatabaseOptions>(
    builder.Configuration.GetSection(WordGameDatabaseOptions.SectionName));
builder.Services.AddSingleton<IWordGameDatabasePathResolver, WordGameDatabasePathResolver>();
builder.Services.AddSingleton<IReadOnlyWordGameConnectionFactory,
    SqliteReadOnlyWordGameConnectionFactory>();
builder.Services.AddSingleton<IVocabularyReadRepository, SqliteVocabularyReadRepository>();
builder.Services.AddSingleton<IPracticeVocabularyRepository,
    SqlitePracticeVocabularyRepository>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPracticeSessionService, PracticeSessionService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapDatabaseEndpoints();
app.MapPracticeEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
