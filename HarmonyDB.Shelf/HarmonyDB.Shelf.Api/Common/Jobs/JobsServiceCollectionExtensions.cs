using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Common.Jobs;

public static class JobsServiceCollectionExtensions
{
    /// <summary>
    /// The queue of background jobs (<see cref="JobQueue"/>) and its worker; safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(JobQueue)))
            return services;

        services.AddOptions<JobsOptions>().Bind(configuration.GetSection(JobsOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<JobsOptions>, JobsOptionsValidator>());
        services.AddSingleton<JobSignal>();
        services.AddScoped<JobQueue>();
        services.AddHostedService<JobWorker>();
        return services;
    }

    /// <summary>
    /// The handler of a kind of jobs: <see cref="JobQueue.EnqueueAsync"/> with this kind has it run.
    /// </summary>
    public static IServiceCollection AddJobHandler<THandler>(this IServiceCollection services, IConfiguration configuration, string kind)
        where THandler : class, IJobHandler
    {
        services.AddJobs(configuration);
        if (services.Any(x => x.ImplementationInstance is JobHandlerRegistration registration && registration.Kind == kind))
            throw new InvalidOperationException($"The jobs of the kind {kind} have a handler already.");

        services.AddSingleton(new JobHandlerRegistration(kind, typeof(THandler)));
        services.AddScoped<THandler>();
        return services;
    }
}
