using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Admin;
using Microsoft.Extensions.Options;

namespace OneShelf.Admin.Web.ShelfAdmin;

/// <summary>
/// The new shelf's internal API for the admin site (<c>/internal/admin</c>, scope <c>shelf.admin</c>), called with the
/// site's internal key from the configuration (<see cref="ShelfAdminOptions"/>).
/// </summary>
public sealed class ShelfAdminClient(HttpClient http, IOptions<ShelfAdminOptions> options)
{
    private const string KeyHeader = "X-Internal-Key";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<AdminPeoplePage> ListPeopleAsync(string? search, string? permission, bool anyPermission, int skip, int take, CancellationToken cancellationToken)
        => SendAsync<AdminPeoplePage>(HttpMethod.Get, $"people?search={Uri.EscapeDataString(search ?? string.Empty)}&permission={Uri.EscapeDataString(permission ?? string.Empty)}&anyPermission={(anyPermission ? "true" : "false")}&skip={skip}&take={take}", null, cancellationToken);

    public Task<AdminPerson> GetPersonAsync(Guid id, CancellationToken cancellationToken)
        => SendAsync<AdminPerson>(HttpMethod.Get, $"people/{id}", null, cancellationToken);

    public Task<AdminPermissionChange> SetPermissionsAsync(Guid id, AdminSetPermissionsRequest request, CancellationToken cancellationToken)
        => SendAsync<AdminPermissionChange>(HttpMethod.Put, $"people/{id}/permissions", request, cancellationToken);

    public Task<AdminPermissionCatalog> GetPermissionCatalogAsync(CancellationToken cancellationToken)
        => SendAsync<AdminPermissionCatalog>(HttpMethod.Get, "permissions", null, cancellationToken);

    public Task<List<AdminPermissionChange>> GetPermissionChangesAsync(Guid? accountId, int take, CancellationToken cancellationToken)
        => SendAsync<List<AdminPermissionChange>>(HttpMethod.Get, $"permission-changes?take={take}{(accountId == null ? null : $"&accountId={accountId}")}", null, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.BaseUrl!), "internal/admin/" + path));
        request.Headers.Add(KeyHeader, options.Value.Key);
        if (body != null)
            request.Content = JsonContent.Create(body, options: Json);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ShelfAdminException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken))!;
    }

    private static async Task<ErrorResponse?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/json")
            return null;

        return await response.Content.ReadFromJsonAsync<ErrorResponse>(Json, cancellationToken);
    }
}

/// <summary>
/// The shelf answered with an error: its status and code.
/// </summary>
public sealed class ShelfAdminException(HttpStatusCode status, ErrorResponse? error)
    : Exception($"The shelf answered {(int)status} {error?.Code}")
{
    public HttpStatusCode Status { get; } = status;

    public string? Code { get; } = error?.Code;

    public IReadOnlyDictionary<string, string>? Details { get; } = error?.Details;
}

/// <summary>
/// The configuration section <c>ShelfAdmin</c>: where the new shelf's API is, and the site's internal key there.
/// </summary>
public sealed class ShelfAdminOptions
{
    public const string SectionName = "ShelfAdmin";

    /// <summary>
    /// The shelf's API, e.g. <c>http://shelf-api/</c>.
    /// </summary>
    public string? BaseUrl { get; set; }

    public string? Key { get; set; }
}

public static class ShelfAdminServiceCollectionExtensions
{
    public static IServiceCollection AddShelfAdminClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ShelfAdminOptions>().Bind(configuration.GetSection(ShelfAdminOptions.SectionName))
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrEmpty(x.Key),
                $"{ShelfAdminOptions.SectionName}: BaseUrl is not an absolute address, or Key is not set.")
            .ValidateOnStart();
        services.AddHttpClient<ShelfAdminClient>();
        return services;
    }
}
