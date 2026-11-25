using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HarmonyDB.Shelf.Model.Jobs;

namespace HarmonyDB.Shelf.Api.Common.Jobs;

/// <summary>
/// The results of the kinds of jobs, as their handlers are registered (<see cref="JobsServiceCollectionExtensions"/>):
/// a result is kept as its own type's JSON, and given to the browser as a <see cref="JobResult"/> told apart by
/// <see cref="Discriminator"/>, the job's kind — in the JSON and in the OpenAPI document alike.
/// </summary>
public sealed class JobResults
{
    public const string Discriminator = "kind";

    private readonly IReadOnlyDictionary<string, Type> _byKind;

    internal JobResults(IEnumerable<JobHandlerRegistration> registrations)
    {
        _byKind = registrations.Where(x => x.ResultType != null).ToDictionary(x => x.Kind, x => x.ResultType!);
    }

    /// <summary>
    /// The kinds and their results' types.
    /// </summary>
    public IReadOnlyDictionary<string, Type> Types => _byKind;

    /// <summary>
    /// The result as it is kept.
    /// </summary>
    /// <exception cref="InvalidOperationException">The kind ends with no result, or with a result of another type.</exception>
    internal string Write(string kind, JobResult result)
    {
        if (!_byKind.TryGetValue(kind, out var type) || result.GetType() != type)
            throw new InvalidOperationException($"A job of the kind {kind} ended with a result of the type {result.GetType().Name}, not the one registered for it.");

        return JsonSerializer.Serialize(result, type, JobQueue.JsonOptions);
    }

    /// <summary>
    /// The result kept, read as its kind's type.
    /// </summary>
    internal JobResult Read(string kind, string json)
        => (JobResult)JsonSerializer.Deserialize(json, _byKind.TryGetValue(kind, out var type)
            ? type
            : throw new InvalidOperationException($"The jobs of the kind {kind} have no result."), JobQueue.JsonOptions)!;

    /// <summary>
    /// Makes <see cref="JobResult"/> written and read with its kind, as one of the registered results.
    /// </summary>
    public void Configure(JsonSerializerOptions options)
    {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(info =>
        {
            if (info.Type != typeof(JobResult))
                return;

            info.PolymorphismOptions = new() { TypeDiscriminatorPropertyName = Discriminator };
            foreach (var (kind, type) in _byKind)
                info.PolymorphismOptions.DerivedTypes.Add(new(type, kind));
        });
    }
}
