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
builder.Services.Configure<WordGameDatabaseOptions>(
    builder.Configuration.GetSection(WordGameDatabaseOptions.SectionName));
builder.Services.AddSingleton<IWordGameDatabasePathResolver, WordGameDatabasePathResolver>();
builder.Services.AddSingleton<IReadOnlyWordGameConnectionFactory,
    SqliteReadOnlyWordGameConnectionFactory>();
builder.Services.AddSingleton<IReadWriteWordGameConnectionFactory,
    SqliteReadWriteWordGameConnectionFactory>();
builder.Services.AddSingleton<IVocabularyReadRepository, SqliteVocabularyReadRepository>();
builder.Services.AddSingleton<IPracticeVocabularyRepository,
    SqlitePracticeVocabularyRepository>();
builder.Services.AddSingleton<IUserProgressRepository, SqliteUserProgressRepository>();
builder.Services.AddSingleton<IRandomSource, SystemRandomSource>();
builder.Services.AddSingleton<IMultipleChoiceService, MultipleChoiceService>();
builder.Services.AddSingleton(TimeProvider.System);
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
