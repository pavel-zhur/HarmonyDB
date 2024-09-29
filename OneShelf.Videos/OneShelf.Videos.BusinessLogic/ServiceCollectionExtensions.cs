using System.Net;
using CasCap.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OneShelf.Videos.BusinessLogic.Models;
using OneShelf.Videos.BusinessLogic.Services;
using OneShelf.Videos.BusinessLogic.Services.Live;
using OneShelf.Videos.Database;
using Polly;

namespace OneShelf.Videos.BusinessLogic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVideosBusinessLogic(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        return serviceCollection
            .AddVideosDatabase(configuration)
            .Configure<VideosOptions>(o => configuration.GetSection(nameof(VideosOptions)).Bind(o))
            .AddScoped<Service1>()
            .AddScoped<Service2>()
            .AddScoped<ExifService>()
            .AddScoped<LiveDownloader>()
            .AddSingleton<TelegramLoggerInitializer>()
            .AddSingleton<Paths>()
            .AddScoped<VideosDatabaseOperations>()
            .AddMyGooglePhotos();
    }

    private static IServiceCollection AddMyGooglePhotos(this IServiceCollection services)
    {
        const string sectionKey = $"{nameof(CasCap)}:{nameof(GooglePhotosOptions)}";

        services
            .AddSingleton<IConfigureOptions<GooglePhotosOptions>>(s =>
            {
                var configuration = s.GetService<IConfiguration?>();
                return new ConfigureOptions<GooglePhotosOptions>(options => configuration?.Bind(sectionKey, options));
            })
            .AddHttpClient<ExtendedGooglePhotosService>((s, client) =>
            {
                var configuration = s.GetRequiredService<IConfiguration>();
                var options = configuration.GetSection(sectionKey).Get<GooglePhotosOptions>();
                options ??= new(); //we use default BaseAddress if no config object injected in
                client.BaseAddress = new(options.BaseAddress);
                client.DefaultRequestHeaders.Add("User-Agent",
                    $"{nameof(CasCap)}.{AppDomain.CurrentDomain.FriendlyName}.{Environment.MachineName}");
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new("deflate"));
                client.Timeout = Timeout.InfiniteTimeSpan;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            .AddTransientHttpErrorPolicy(policyBuilder => { return policyBuilder.RetryAsync(retryCount: 3); })
            //https://github.com/aspnet/AspNetCore/issues/6804
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        return services;
    }
}