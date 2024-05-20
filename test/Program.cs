using HarmonyDB.Index.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.Database.Songs;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Illustrations.Database;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Processor;

namespace test
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration.AddJsonFile("appsettings.Secrets.json", true);

            builder.Services
                .AddLogging()
                .AddIllustrationsDatabase(builder.Configuration)
                .AddSongsDatabase()
                .AddBillingApiClient(builder.Configuration)
                .AddIllustrationsApiClient(builder.Configuration)
                .AddProcessor(builder.Configuration)
                .AddIndexAnalysis()
                .AddDogDatabase();
            var host = builder.Build();

            await host.Services.GetRequiredService<SongsDatabase>().Database.MigrateAsync();

        }
    }
}
