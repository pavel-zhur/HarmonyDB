using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OneShelf.Common.Api.Hosting;

/// <summary>
/// A host of API modules: one web application whose requests under a module's path base go to that module.
/// <code>
/// var builder = WebApplication.CreateBuilder(args);
/// builder.AddApiModules(new FirstModule(), new SecondModule());
/// var app = builder.Build();
/// app.MapApiModules();
/// app.Run();
/// </code>
/// </summary>
public static class ApiHostExtensions
{
    /// <summary>
    /// The largest request body accepted, as the Azure Functions host accepted.
    /// </summary>
    private const long MaxRequestBodySize = 100 * 1024 * 1024;

    public static WebApplicationBuilder AddApiModules(this WebApplicationBuilder builder, params ApiModule[] modules)
    {
        if (modules.Select(x => x.Name.ToLowerInvariant()).Distinct().Count() != modules.Length)
            throw new ArgumentException("The names of the modules must differ.", nameof(modules));

        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBodySize);

        // The requests, the calls the modules make and the host's own logs (with APPLICATIONINSIGHTS_CONNECTION_STRING).
        builder.Services.AddApplicationInsightsTelemetry();

        builder.Services
            .AddSingleton(new ApiModules(modules.Select(x => new ApiModuleHost(x)).ToList()))
            .AddHostedService<ApiModulesService>();

        return builder;
    }

    public static WebApplication MapApiModules(this WebApplication application)
    {
        foreach (var module in application.Services.GetRequiredService<ApiModules>().Hosts)
        {
            application.Map(module.PathBase, branch => branch.Run(module.ProcessRequestAsync));
        }

        return application;
    }
}

internal sealed record ApiModules(IReadOnlyList<ApiModuleHost> Hosts);

/// <summary>
/// Starts the modules before the host starts serving, and stops them after it has stopped.
/// </summary>
internal sealed class ApiModulesService : IHostedLifecycleService
{
    private readonly ApiModules _modules;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ApiModulesService(ApiModules modules, IConfiguration configuration, IHostEnvironment environment)
    {
        _modules = modules;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        foreach (var module in _modules.Hosts)
        {
            await module.StartAsync(_configuration, _environment, cancellationToken);
        }
    }

    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        foreach (var module in _modules.Hosts.Reverse())
        {
            await module.StopAsync(cancellationToken);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
