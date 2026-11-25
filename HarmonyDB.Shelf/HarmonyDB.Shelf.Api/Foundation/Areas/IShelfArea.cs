namespace HarmonyDB.Shelf.Api.Foundation.Areas;

/// <summary>
/// An area of the API (auth, me, playlists …): one class that registers the area's services and maps its endpoints,
/// beside a folder of its services. Added to the app by one line in Program.cs: <c>builder.AddArea&lt;TArea&gt;()</c>.
/// </summary>
public interface IShelfArea
{
    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>
    /// The area's middleware, if it has any: it runs after authentication and before authorization, in the order the
    /// areas were added. An area's authentication schemes are services: they are added in <see cref="AddServices"/>.
    /// </summary>
    void UseMiddleware(IApplicationBuilder app)
    {
    }
}
