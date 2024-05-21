using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OneShelf.Common.Api;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureApi(this IHostBuilder hostBuilder,
        Action<HostBuilderContext, IFunctionsWorkerApplicationBuilder>? configureWorker = null)
        => hostBuilder
            .ConfigureFunctionsWebApplication((builderContext, builder) =>
            {
                builder.UseMiddleware<ErrorHandlingMiddleware>();
                configureWorker?.Invoke(builderContext, builder);
            }).ConfigureServices(services =>
            {
                services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
                {
                });
            });
}