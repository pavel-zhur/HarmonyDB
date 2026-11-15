using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api.Background;

/// <summary>
/// Work run on a schedule, created in a scope of its own for every run.
/// </summary>
public interface IScheduledWork
{
    Task Run(CancellationToken cancellationToken);
}

internal sealed class ScheduleService<TWork> : BackgroundService
    where TWork : class, IScheduledWork
{
    private readonly string _name;
    private readonly CronExpression _schedule;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ScheduleService<TWork>> _logger;

    public ScheduleService(string name, CronExpression schedule, IServiceScopeFactory serviceScopeFactory, ILogger<ScheduleService<TWork>> logger)
    {
        _name = name;
        _schedule = schedule;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (_schedule.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc) is { } next)
        {
            _logger.LogInformation("{schedule}: the next run is at {next:u}.", _name, next);

            try
            {
                await Task.Delay(next - DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TWork>().Run(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{schedule}: the run failed.", _name);
            }
        }
    }
}

public static class ScheduleServiceCollectionExtensions
{
    /// <summary>
    /// Runs <typeparamref name="TWork"/> on a schedule: a cron expression with seconds (<c>second minute hour day month weekday</c>)
    /// in UTC, as the timer triggers of Azure Functions had. Whether it runs is the setting <c>Schedules:{name}</c>
    /// (true or false), which is required.
    /// </summary>
    public static IServiceCollection AddSchedule<TWork>(this IServiceCollection services, IConfiguration configuration, string name, string cron)
        where TWork : class, IScheduledWork
    {
        var schedule = CronExpression.Parse(cron, CronFormat.IncludeSeconds);
        var isEnabled = configuration.GetValue<bool?>($"Schedules:{name}")
                        ?? throw new($"The setting Schedules:{name} (true or false) is required.");

        services.AddScoped<TWork>();

        if (isEnabled)
        {
            services.AddHostedService(sp => new ScheduleService<TWork>(
                name,
                schedule,
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<ScheduleService<TWork>>>()));
        }

        return services;
    }
}
