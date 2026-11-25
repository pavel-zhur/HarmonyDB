using HarmonyDB.Shelf.Api.Areas.PublicPage.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.PublicPage;

/// <summary>
/// What the public page's server writes into the page on the server, for guests and crawlers that run no script:
/// the head's preview of an address (its title, description and picture), put by the page's nginx into the page's own
/// HTML in place of its generic head (no answer: the page keeps its own); and the page's sitemap. Not for the browser's
/// client: left out of its document.
/// </summary>
public sealed class PublicPageArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PublicPageOptions>().Bind(configuration.GetSection(PublicPageOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PublicPageOptions>, PublicPageOptionsValidator>());
        services.AddMemoryCache();
        services.AddScoped<PagePreviews>();
        services.AddScoped<Sitemap>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public").ExcludeFromDescription();

        // the page's address after the prefix: /api/public/preview/s/{token} is the preview of /s/{token}
        group.MapGet("/preview/{**path}", async (HttpContext context, string? path, PagePreviews previews, CancellationToken cancellationToken) =>
        {
            var html = await previews.GetAsync("/" + (path ?? string.Empty).TrimEnd('/'), cancellationToken);
            if (html == null)
                return Results.NoContent();

            context.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Content(html, "text/html; charset=utf-8");
        }).WithName("GetPublicPagePreview");

        group.MapGet("/sitemap", async (HttpContext context, Sitemap sitemap, CancellationToken cancellationToken) =>
        {
            var xml = await sitemap.GetAsync(cancellationToken);
            if (xml == null)
                return Results.NotFound();

            context.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.File(xml, "application/xml; charset=utf-8");
        }).WithName("GetPublicPageSitemap");
    }
}
