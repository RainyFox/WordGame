using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordGame.Web.Services;

namespace WordGame.Web.Tests.Support;

internal static class WordGameWebApplicationFactory
{
    public static WebApplicationFactory<Program> Create(
        string databasePath,
        TimeProvider? timeProvider = null,
        IRandomSource? randomSource = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Path"] = databasePath
                });
            });
            builder.ConfigureServices(services =>
            {
                if (timeProvider is not null)
                    services.AddSingleton(timeProvider);
                if (randomSource is not null)
                    services.AddSingleton(randomSource);
            });
        });
    }
}
