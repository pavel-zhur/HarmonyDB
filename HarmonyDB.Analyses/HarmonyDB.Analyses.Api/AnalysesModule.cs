using HarmonyDB.Analyses.Api.Models;
using HarmonyDB.Analyses.Api.Services;
using HarmonyDB.Analyses.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api.Hosting;

namespace HarmonyDB.Analyses.Api;

/// <summary>
/// The analyses: a source of the contract, version 2, for the index, and the place they are put in. An analysis with
/// all its content and files is one item here, open to all. The module keeps its own data apart: its schema of the
/// common database, reached by its own role (connection string <see cref="AnalysesDatabase.ConnectionStringName"/>),
/// whose tables its migrations make when it starts; and its own folder of files. It calls no one.
/// </summary>
public class AnalysesModule() : ApiModule("Analyses")
{
    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<AnalysesOptions>().Bind(builder.Configuration.GetSection(AnalysesOptions.SectionName)).ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AnalysesOptions>, AnalysesOptionsValidator>());

        var connectionString = builder.Configuration.GetConnectionString(AnalysesDatabase.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"The connection string {AnalysesDatabase.ConnectionStringName} is not set.");

        builder.Services.AddDbContext<AnalysesDatabase>(options => AnalysesDatabase.Configure(options, connectionString));
        builder.Services.AddHostedService<MigrateOnStart>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AnalysisFolder>();
        builder.Services.AddScoped<AnalysisStore>();
        builder.Services.AddScoped<AnalysesSource>();
    }

    /// <summary>
    /// Brings the module's tables up to date before it serves.
    /// </summary>
    private sealed class MigrateOnStart(IServiceProvider services) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AnalysesDatabase>().Database.MigrateAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
