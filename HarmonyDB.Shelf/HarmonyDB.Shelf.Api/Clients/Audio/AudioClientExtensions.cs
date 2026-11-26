using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Clients.Audio;

public static class AudioClientExtensions
{
    /// <summary>
    /// The music service's client. Safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddAudioClient(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(AudioClient)))
            return services;

        services.AddOptions<AudioClientOptions>().Bind(configuration.GetSection(AudioClientOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AudioClientOptions>, AudioClientOptionsValidator>());
        // each call has its own timeout (AudioClient.DefaultTimeout, or longer where it says)
        services.AddHttpClient<AudioClient>(x => x.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }
}
