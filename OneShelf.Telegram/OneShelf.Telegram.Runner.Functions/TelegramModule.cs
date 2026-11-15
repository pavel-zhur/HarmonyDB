using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api.Background;
using OneShelf.Common.Api.Hosting;
using OneShelf.Telegram.Processor;
using OneShelf.Telegram.Runner.Functions.Background;

namespace OneShelf.Telegram.Runner.Functions;

/// <summary>
/// The library bot.
/// </summary>
public class TelegramModule() : ApiModule("Telegram")
{
    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });

        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

        builder.Services
            .AddProcessor(builder.Configuration)
            .AddBackgroundQueue<string, UpdatesWork>(new()
            {
                MaxConcurrency = 16,
                MaxAttempts = 5,
                RetryDelay = TimeSpan.FromSeconds(30),
            })
            .AddSchedule<HourlyWork>(builder.Configuration, "Hourly", "0 15 * * * *") // at minute 15 of every hour
            .AddSchedule<DailySelectionWork>(builder.Configuration, "DailySelection", "0 34 9 * * *"); // at 9:34 UTC (12:34 UTC+3)
    }
}
