using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace OneShelf.Common.Api.Hosting;

/// <summary>
/// The server of a module: it listens on nothing. The host hands it the requests for the module's path base,
/// and it runs them through the module's application.
/// </summary>
internal sealed class ModuleServer : IServer
{
    private Func<IFeatureCollection, Task>? _process;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        where TContext : notnull
    {
        _process = async features =>
        {
            var context = application.CreateContext(features);
            try
            {
                await application.ProcessRequestAsync(context);
            }
            catch (Exception e)
            {
                application.DisposeContext(context, e);
                throw;
            }

            application.DisposeContext(context, null);
        };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _process = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs a request of the host through the module. The module sees the request's features (its path relative to
    /// the module's path base, its body, its response) through a collection of its own, so what it sets for itself
    /// (its services, its route) stays with it.
    /// </summary>
    public Task ProcessRequestAsync(HttpContext hostContext)
    {
        var process = _process;
        if (process == null)
        {
            hostContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }

        return process(new FeatureCollection(hostContext.Features));
    }

    public void Dispose()
    {
    }
}
