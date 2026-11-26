using HarmonyDB.Shelf.Model.Jobs;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

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
        services.AddSingleton(provider => new JobResults(provider.GetServices<JobHandlerRegistration>()));
        services.AddSingleton(provider => new JobKinds(provider.GetServices<JobHandlerRegistration>()));
        services.AddScoped<JobQueue>();
        services.AddHostedService<JobWorker>();

        // the results, one type per kind, told apart by the kind: in the browser's JSON and in its OpenAPI document
        services.AddOptions<JsonOptions>().Configure<JobResults>((options, results) => results.Configure(options.SerializerOptions));
        services.AddOptions<SwaggerGenOptions>().Configure<JobResults>((options, results) =>
        {
            options.UseOneOfForPolymorphism();
            options.SelectSubTypesUsing(type => type == typeof(JobResult) ? results.Types.Values : []);
            options.SelectDiscriminatorNameUsing(type => type == typeof(JobResult) ? JobResults.Discriminator : null);
            options.SelectDiscriminatorValueUsing(type => results.Types.SingleOrDefault(x => x.Value == type).Key);
            options.SchemaFilter<JobResultKindFilter>(results);
        });

        return services;
    }

    /// <summary>
    /// Each result's schema has its kind, as the JSON writes it.
    /// </summary>
    private sealed class JobResultKindFilter(JobResults results) : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (results.Types.SingleOrDefault(x => x.Value == context.Type).Key is not { } kind)
                return;

            schema.Properties[JobResults.Discriminator] = new() { Type = "string", Enum = [new Microsoft.OpenApi.Any.OpenApiString(kind)] };
            schema.Required.Add(JobResults.Discriminator);
        }
    }

    /// <summary>
    /// The handler of a kind of jobs that ends with no result: <see cref="JobQueue.EnqueueAsync"/> with this kind has it
    /// run.
    /// </summary>
    public static IServiceCollection AddJobHandler<THandler>(this IServiceCollection services, IConfiguration configuration, string kind)
        where THandler : class, IJobHandler
        => services.Register<THandler>(configuration, kind, null);

    /// <summary>
    /// The handler of a kind of jobs that ends with a result of its own type: the browser is given it as a
    /// <see cref="JobResult"/> of this kind.
    /// </summary>
    public static IServiceCollection AddJobHandler<THandler, TResult>(this IServiceCollection services, IConfiguration configuration, string kind)
        where THandler : class, IJobHandler
        where TResult : JobResult
        => services.Register<THandler>(configuration, kind, typeof(TResult));

    /// <summary>
    /// The handler of a kind of the shelf's own follow-ups (<see cref="JobLane.FollowUp"/>), which end with no result:
    /// <see cref="JobQueue.QueueFollowUp"/> with this kind has it run.
    /// </summary>
    public static IServiceCollection AddFollowUpJobHandler<THandler>(this IServiceCollection services, IConfiguration configuration, string kind)
        where THandler : class, IJobHandler
        => services.Register<THandler>(configuration, kind, null, JobLane.FollowUp);

    private static IServiceCollection Register<THandler>(this IServiceCollection services, IConfiguration configuration, string kind, Type? resultType,
        JobLane lane = JobLane.Person)
        where THandler : class, IJobHandler
    {
        services.AddJobs(configuration);
        if (services.Any(x => x.ImplementationInstance is JobHandlerRegistration registration && registration.Kind == kind))
            throw new InvalidOperationException($"The jobs of the kind {kind} have a handler already.");

        services.AddSingleton(new JobHandlerRegistration(kind, typeof(THandler), resultType, lane));
        services.AddScoped<THandler>();
        return services;
    }
}
