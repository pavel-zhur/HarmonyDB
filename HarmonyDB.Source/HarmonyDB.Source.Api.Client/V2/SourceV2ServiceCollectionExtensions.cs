using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Source.Api.Client.V2;

public static class SourceV2ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="SourceV2Client"/> for the one server described by <paramref name="section"/>
    /// (<see cref="SourceV2ClientOptions"/>: <c>Name</c>, <c>Endpoint</c>, <c>Key</c>, optional <c>Timeout</c>).
    /// </summary>
    public static IServiceCollection AddSourceV2Client(this IServiceCollection services, IConfigurationSection section)
    {
        var options = new SourceV2ClientOptions();
        section.Bind(options);
        SourceV2Client.Validate(options);
        services.AddSingleton(options);

        // the client keeps its own timeout per operation
        services.AddHttpClient<SourceV2Client>(client => client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }
}
