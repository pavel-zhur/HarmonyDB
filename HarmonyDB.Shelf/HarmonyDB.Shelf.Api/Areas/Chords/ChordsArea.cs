using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Source.Api.Model.V2;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HarmonyDB.Shelf.Api.Areas.Chords;

/// <summary>
/// Items of chords from the sources, for the person (or guest) asking: their content in the kinds asked, their
/// headers with the sources' labels as given to this person, and the sources' pictures. Open to guests: chords are
/// never private, a source decides what is open to whom.
/// </summary>
public sealed class ChordsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddChordsReading(configuration);

        // the chords' content is the source contract's own models: their schemas are named apart from the browser
        // API's (both have a "MusicService", for one)
        services.Configure<SwaggerGenOptions>(x => x.CustomSchemaIds(SchemaId));
    }

    /// <summary>
    /// Swashbuckle's own naming of a schema, with the source contract's models named "SourceV2…".
    /// </summary>
    private static string SchemaId(Type type)
    {
        var name = type.IsConstructedGenericType
            ? string.Concat(type.GetGenericArguments().Select(SchemaId)) + type.Name.Split('`')[0]
            : type.Name.Replace("[]", "Array");
        return type.Namespace?.StartsWith("HarmonyDB.Source.Api.Model.V2", StringComparison.Ordinal) == true ? $"SourceV2{name}" : name;
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chords").WithTags("Chords");

        // The sources this person may see; before /{id}, which would take "sources" for an id.
        group.MapGet("/sources", (ClaimsPrincipal user, ChordsReader reader, CancellationToken cancellationToken)
                => reader.GetSourcesAsync(user, cancellationToken))
            .AllowGuests()
            .WithName("GetChordsSources");

        // The kinds are given as "sheet,timeline"; the sheet when none is given. The address: the one the person came
        // by (a link opened), when they came by one.
        group.MapGet("/{id}", (ClaimsPrincipal user, string id, string? kinds, string? address, ChordsReader reader, CancellationToken cancellationToken)
                => reader.GetContentAsync(user, id, ParseKinds(kinds), ParseAddress(address), cancellationToken))
            .WithName("GetChords");

        // A read of many items at once: a POST for the length of the list.
        group.MapPost("/headers", async (ClaimsPrincipal user, ChordsHeadersRequest request, ChordsReader reader, CancellationToken cancellationToken)
                => await reader.GetHeadersWithAnnotationsAsync(user, request.Ids ?? [], cancellationToken))
            .AllowGuests()
            .WithName("GetChordsHeaders");

        group.MapGet("/icons/{name}", async (HttpContext context, string name, SourceIcons icons, CancellationToken cancellationToken) =>
            {
                var icon = await icons.GetAsync(name, cancellationToken);
                context.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.File(icon.Content, icon.ContentType);
            })
            .WithName("GetSourceIcon");
    }

    /// <summary>
    /// The address a person came by, as the browser gives it: absolute, else 400 <c>bad_request</c>; null when none.
    /// </summary>
    public static Uri? ParseAddress(string? address)
        => string.IsNullOrEmpty(address) ? null
            : Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri
            : throw ShelfException.BadRequest();

    /// <summary>
    /// The kinds asked as "sheet,timeline"; the sheet when none is given. An unknown one answers 400 <c>unknown_kind</c>.
    /// </summary>
    public static IReadOnlyList<ContentKind> ParseKinds(string? kinds)
    {
        if (string.IsNullOrWhiteSpace(kinds))
            return [ContentKind.Sheet];

        return kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Enum.TryParse<ChordsKind>(x, true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(x, out _)
                ? ChordsReader.ToContentKind(kind)
                : throw ShelfException.BadRequest(ChordsErrorCodes.UnknownKind, new Dictionary<string, string> { ["kind"] = x }))
            .Distinct()
            .ToList();
    }
}

public static class ChordsReadingExtensions
{
    /// <summary>
    /// What reads chords from the sources for a person (<see cref="ChordsReader"/>, <see cref="SourceCallers"/>,
    /// <see cref="SourceIcons"/>); safe to call from every area that needs it.
    /// </summary>
    public static IServiceCollection AddChordsReading(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(ChordsReader)))
            return services;

        services.AddMemoryCache();
        services.AddIndexClient(configuration);
        services.AddItemRows();
        services.AddShelfCommon();
        services.AddScoped<SourceCallers>();
        services.AddScoped<ChordsReader>();
        services.AddScoped<SourceIcons>();
        return services;
    }
}
