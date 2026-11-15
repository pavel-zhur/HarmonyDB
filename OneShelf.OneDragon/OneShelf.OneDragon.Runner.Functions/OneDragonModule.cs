using Microsoft.AspNetCore.Builder;
using OneShelf.Common.Api.Background;
using OneShelf.Common.Api.Hosting;
using OneShelf.OneDragon.Processor;
using OneShelf.OneDragon.Runner.Functions.Background;

namespace OneShelf.OneDragon.Runner.Functions;

public class OneDragonModule() : ApiModule("OneDragon")
{
    public override void Configure(WebApplicationBuilder builder) => builder.Services
        .AddProcessor(builder.Configuration)
        .AddBackgroundQueue<string, UpdatesWork>(new()
        {
            MaxConcurrency = 16,
            MaxAttempts = 5,
            RetryDelay = TimeSpan.FromSeconds(30),
        });
}
