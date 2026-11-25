using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Book.Services;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Clients.Pdfs;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Book;

namespace HarmonyDB.Shelf.Api.Areas.Book;

/// <summary>
/// The PDF book, for the people with the permission <see cref="BuiltInPermissions.Pdf"/>: from a playlist, a search,
/// a search inside a playlist or one item — always "these items, in this order, with these options"
/// (<see cref="BookRequest"/>). Both are jobs: counting the pages of each item, to lay the book out, and making the
/// book, whose PDF is the job's file (<c>/api/jobs/{id}/file</c>). The results' shapes: <see cref="BookPagesResult"/>,
/// <see cref="BookResult"/>.
/// </summary>
public sealed class BookArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddPdfsClient(configuration);
        services.AddChordsReading(configuration);
        services.AddSingleton<BookPdfCache>();
        services.AddScoped<BookPrinter>();
        services.AddScoped<BookService>();
        services.AddJobHandler<BookPagesJob>(configuration, BookService.PagesJobKind);
        services.AddJobHandler<BookJob>(configuration, BookService.BookJobKind);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/book").WithTags("Book").RequireSignIn().RequirePermission(BuiltInPermissions.Pdf);

        group.MapPost("/pages", (ClaimsPrincipal user, BookRequest request, BookService service, CancellationToken cancellationToken)
            => service.StartPagesAsync(user, request, cancellationToken)).WithName("CountBookPages");

        group.MapPost("", (ClaimsPrincipal user, BookRequest request, BookService service, CancellationToken cancellationToken)
            => service.StartBookAsync(user, request, cancellationToken)).WithName("MakeBook");
    }
}
