using System.Diagnostics;
using HarmonyDB.Common.ServiceAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api.Hosting;

/// <summary>
/// Runs one module: its own web application, with its own services and settings, served by a <see cref="ModuleServer"/>.
/// </summary>
internal sealed class ApiModuleHost
{
    private readonly ModuleServer _server = new();
    private WebApplication? _application;

    public ApiModuleHost(ApiModule module)
    {
        Module = module;
        PathBase = $"/{module.Name.ToLowerInvariant()}";
    }

    public ApiModule Module { get; }

    public PathString PathBase { get; }

    public async Task StartAsync(IConfiguration hostConfiguration, IHostEnvironment hostEnvironment, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = Module.GetType().Assembly.GetName().Name,
            EnvironmentName = hostEnvironment.EnvironmentName,
            ContentRootPath = hostEnvironment.ContentRootPath,
        });

        // The module's settings: the host's, and over them the host's section named after the module.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddConfiguration(hostConfiguration);
        builder.Configuration.AddConfiguration(hostConfiguration.GetSection(Module.Name));

        builder.Services.AddSingleton<IServer>(_server);

        // The host reports the requests (it logs them and tracks them in Application Insights), so the module's
        // application does not report them again.
        builder.Services.AddSingleton(new DiagnosticListener(typeof(ApiModuleHost).FullName!));
        builder.Services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        var applicationInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
        {
            builder.Logging.AddApplicationInsights(
                configuration => configuration.ConnectionString = applicationInsightsConnectionString,
                _ => { });
        }

        // The internal clients that may call the module's functions with an internal scope (setting InternalClients).
        builder.Services.AddInternalClients(builder.Configuration);

        builder.Services
            .Configure<FunctionKeysOptions>(o => builder.Configuration.GetSection(FunctionKeysOptions.SectionName).Bind(o.Keys))
            .AddSingleton<IObjectModelValidator, NoObjectModelValidator>()
            .AddControllers(options =>
            {
                options.Conventions.Add(new FunctionsOnlyConvention());
                options.Filters.Add<FunctionAccessFilter>();
                options.Filters.Add<BindingFailureFilter>();
                foreach (var filter in options.Filters.OfType<UnsupportedContentTypeFilter>().ToList()) options.Filters.Remove(filter);
                options.ModelBinderProviders.Insert(0, new HttpRequestModelBinderProvider());
                options.ModelMetadataDetailsProviders.Add(new BindingSourceMetadataProvider(typeof(HttpRequest), BindingSource.Special));
            })
            .ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(Module.GetType().Assembly));
                manager.FeatureProviders.Add(new FunctionControllerFeatureProvider());
            });

        Module.Configure(builder);

        _application = builder.Build();
        _application.UseMiddleware<ErrorHandlingMiddleware>();
        _application.UseRouting();
        _application.MapControllers();

        await _application.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_application == null) return;

        await _application.StopAsync(cancellationToken);
        await _application.DisposeAsync();
        _application = null;
    }

    public Task ProcessRequestAsync(HttpContext context) => _server.ProcessRequestAsync(context);
}
