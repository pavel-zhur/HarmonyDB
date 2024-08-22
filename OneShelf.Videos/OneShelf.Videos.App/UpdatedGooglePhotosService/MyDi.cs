using System.Net;
using CasCap.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace OneShelf.Videos.App.UpdatedGooglePhotosService;

public static class ServiceCollectionExtensions
{
    public static void AddMyGooglePhotos(this IServiceCollection services)
        => services.AddMyGooglePhotos(_ => { });

    private static readonly string SectionKey = $"{nameof(CasCap)}:{nameof(GooglePhotosOptions)}";

    public static void AddMyGooglePhotos(this IServiceCollection services, Action<GooglePhotosOptions> configure)
    {
        services.AddSingleton<IConfigureOptions<GooglePhotosOptions>>(s =>
        {
            var configuration = s.GetService<IConfiguration?>();
            return new ConfigureOptions<GooglePhotosOptions>(options => configuration?.Bind(SectionKey, options));
        });
        services.AddHttpClient<UpdatedGooglePhotosService>((s, client) =>
            {
                var configuration = s.GetRequiredService<IConfiguration>();
                var options = configuration.GetSection(SectionKey).Get<GooglePhotosOptions>();
                options ??= new();//we use default BaseAddress if no config object injected in
                client.BaseAddress = new(options.BaseAddress);
                client.DefaultRequestHeaders.Add("User-Agent", $"{nameof(CasCap)}.{AppDomain.CurrentDomain.FriendlyName}.{Environment.MachineName}");
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new("deflate"));
                client.Timeout = Timeout.InfiniteTimeSpan;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            .AddTransientHttpErrorPolicy(policyBuilder =>
            {
                return policyBuilder.RetryAsync(retryCount: 3);
            })
            //https://github.com/aspnet/AspNetCore/issues/6804
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }
}