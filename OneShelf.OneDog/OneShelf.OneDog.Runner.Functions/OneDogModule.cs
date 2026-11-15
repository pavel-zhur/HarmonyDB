using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using OneShelf.Common.Api.Background;
using OneShelf.Common.Api.Hosting;
using OneShelf.OneDog.Processor;
using OneShelf.OneDog.Runner.Functions.Background;

namespace OneShelf.OneDog.Runner.Functions;

public class OneDogModule() : ApiModule("OneDog")
{
    public override void Configure(WebApplicationBuilder builder)
    {
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });

        builder.Services
            .AddProcessor(builder.Configuration)
            .AddBackgroundQueue<DomainUpdate, UpdatesWork>(new()
            {
                MaxConcurrency = 16,
                MaxAttempts = 5,
                RetryDelay = TimeSpan.FromSeconds(30),
            });
    }
}
