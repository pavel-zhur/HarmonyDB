using System.Net;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Areas.Book.Services;
using HarmonyDB.Shelf.Api.Clients.Pdfs;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Book;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Shelf.Tests.Jobs;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Book;

/// <summary>
/// The book: which items are printed and with which numbers; each item's key by the person's keys; who may ask.
/// </summary>
public sealed class BookTests(WorkFixture fixture) : IClassFixture<WorkFixture>
{
    [Fact]
    public void TheItemsPrintedAreNumberedAnewOrKeepTheirNumbers()
    {
        var items = new[]
        {
            Prepared("a", BookItemState.Printed, number: 4),
            Prepared("b", BookItemState.NotOpen, number: 5),
            Prepared("c", BookItemState.Printed, number: 7),
            Prepared("d", BookItemState.Printed, number: null),
            Prepared("e", BookItemState.Unavailable, number: 9),
        };

        Assert.Equal([1, null, 2, 3, null], BookJob.Numbers(items, renumber: true));
        Assert.Equal([4, null, 7, 4, null], BookJob.Numbers(items, renumber: false));
    }

    [DatabaseFact]
    public async Task ThePagesOfTheContentsAreCountedWhenTheBookHasThem()
    {
        var me = await fixture.AccountAsync();
        fixture.Index.Answer = (operation, body) => operation == "V2GetContent"
            ? (HttpStatusCode.OK, new GetContentResponse
            {
                Id = body.GetProperty("id").GetString()!,
                State = ItemState.Open,
                Header = FakeIndex.Header(body.GetProperty("id").GetString()!, "Source"),
                Content = new() { Sheet = new() { Root = new NodeHtml { Name = "pre" } } },
            })
            : (HttpStatusCode.InternalServerError, null);

        // the contents take 3 pages, a song 1
        using var api = fixture.Api.WithWebHostBuilder(x => x
            .UseSetting("Clients:Pdfs:Endpoint", "http://pdfs.test/api/")
            .UseSetting("Clients:Pdfs:Key", FakeService.Key)
            .ConfigureTestServices(services => services.AddHttpClient<PdfsClient>().ConfigurePrimaryHttpMessageHandler(() => new FakePdfs())));

        async Task<BookPagesResult> CountAsync(BookOptions options)
        {
            var request = new BookRequest { Items = [new() { ChordsId = $"book-{Guid.NewGuid():N}" }, new() { ChordsId = $"book-{Guid.NewGuid():N}" }], Options = options };
            var job = new Job
            {
                Id = Guid.NewGuid(), AccountId = me, Kind = BookService.PagesJobKind, State = JobState.Running, CreatedAt = DateTimeOffset.UtcNow, Attempts = 1,
                Input = System.Text.Json.JsonSerializer.Serialize(new BookJobInput(request, []), global::HarmonyDB.Shelf.Api.Common.Jobs.JobQueue.JsonOptions),
            };
            await using var scope = api.Services.CreateAsyncScope();
            var completion = await scope.ServiceProvider.GetRequiredService<BookPagesJob>()
                .RunAsync(new(job, api.Services.GetRequiredService<IServiceScopeFactory>()), CancellationToken.None);
            return Assert.IsType<BookPagesResult>(completion.Result);
        }

        var withContents = await CountAsync(new() { Caption = "Songs", Contents = true });
        Assert.Equal(3, withContents.ContentsPages);
        Assert.All(withContents.Items, x => Assert.Equal((1, 1), (x.PagesOneColumn, x.PagesTwoColumns)));

        Assert.Null((await CountAsync(new() { Caption = "Songs" })).ContentsPages);
        Assert.Null((await CountAsync(new() { Contents = true })).ContentsPages);
    }

    private sealed class FakePdfs : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var html = await request.Content!.ReadAsStringAsync(cancellationToken);
            var content = new ByteArrayContent([1]);
            content.Headers.ContentDisposition = new("attachment") { FileName = html.Contains("<table class='contents'>") ? "3.pdf" : "1.pdf" };
            return new(HttpStatusCode.OK) { Content = content };
        }
    }

    [DatabaseFact]
    public async Task EachItemIsInTheKeyAskedElseHereElseMineElseAsWritten()
    {
        var me = await fixture.AccountAsync();
        var other = await fixture.AccountAsync();
        var (playlist, elsewhere) = (Guid.NewGuid(), Guid.NewGuid());
        var ids = Enumerable.Range(0, 5).Select(i => $"book-{Guid.NewGuid():N}-{i}").ToList();
        await using (var db = fixture.CreateContext())
        {
            foreach (var id in new[] { playlist, elsewhere })
                db.Playlists.Add(new() { Id = id, OwnerId = me, Title = "A playlist", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var chords = ids.Select(x => new ChordsItem { Id = Guid.NewGuid(), SourceId = x, CreatedAt = DateTimeOffset.UtcNow }).ToList();
            db.Chords.AddRange(chords);

            void Key(Guid account, int item, Guid? inPlaylist, int semitones)
                => db.Transpositions.Add(new() { Id = Guid.NewGuid(), AccountId = account, ChordsId = chords[item].Id, PlaylistId = inPlaylist, Semitones = semitones, UpdatedAt = DateTimeOffset.UtcNow });

            Key(me, 0, null, 2);
            Key(me, 0, playlist, 5);
            Key(other, 0, playlist, 7);     // another member's "here" is theirs
            Key(me, 1, null, 3);
            Key(me, 3, elsewhere, 4);       // "here" of another playlist does not count
            Key(me, 4, null, 2);
            await db.SaveChangesAsync();
        }

        fixture.Index.Answer = (operation, body) => operation == "V2GetContent"
            ? (HttpStatusCode.OK, new GetContentResponse
            {
                Id = body.GetProperty("id").GetString()!,
                State = ItemState.Open,
                Header = FakeIndex.Header(body.GetProperty("id").GetString()!, "Source"),
                Content = new() { Sheet = new() { Root = new NodeHtml { Name = "pre" } } },
            })
            : (HttpStatusCode.InternalServerError, null);

        await using var scope = fixture.Api.Services.CreateAsyncScope();
        var printer = scope.ServiceProvider.GetRequiredService<BookPrinter>();
        var request = new BookRequest
        {
            PlaylistId = playlist,
            Items = ids.Select((x, i) => new BookItem { ChordsId = x, Transpose = i == 4 ? -1 : null }).ToList(),
            Options = new(),
        };

        var prepared = await printer.PrepareAsync(me, request, [], _ => Task.CompletedTask, CancellationToken.None);
        Assert.Equal([5, 3, 0, 0, -1], prepared.Select(x => x.Transpose));
        Assert.All(prepared, x => Assert.Equal(BookItemState.Printed, x.State));
    }

    [DatabaseFact]
    public async Task ASheetItsSourceDoesNotVouchForIsLeftOut()
    {
        var me = await fixture.AccountAsync();
        var (stable, unstable) = ($"book-{Guid.NewGuid():N}", $"book-{Guid.NewGuid():N}");
        fixture.Index.Answer = (operation, body) => operation == "V2GetContent"
            ? (HttpStatusCode.OK, new GetContentResponse
            {
                Id = body.GetProperty("id").GetString()!,
                State = ItemState.Open,
                Header = FakeIndex.Header(body.GetProperty("id").GetString()!, "Source"),
                Content = new() { Sheet = new() { Root = new NodeHtml { Name = "pre" }, UnstableMessage = body.GetProperty("id").GetString() == unstable ? "Its words" : null } },
            })
            : (HttpStatusCode.InternalServerError, null);

        await using var scope = fixture.Api.Services.CreateAsyncScope();
        var printer = scope.ServiceProvider.GetRequiredService<BookPrinter>();
        var request = new BookRequest { Items = [new() { ChordsId = stable }, new() { ChordsId = unstable }], Options = new() };

        var prepared = await printer.PrepareAsync(me, request, [], _ => Task.CompletedTask, CancellationToken.None);
        Assert.Equal([BookItemState.Printed, BookItemState.Unstable], prepared.Select(x => x.State));
        Assert.Null(prepared[1].Root);
    }

    [Fact]
    public void TheLineNamesTheSourceShortlyWhereItGivesAShortName()
    {
        PreparedItem Item(string? shortSource) => new(
            new() { ChordsId = "x" }, BookItemState.Printed, 0,
            FakeIndex.Header("x", "The source") with { Marks = new() { ShortSource = shortSource } }, new NodeHtml { Name = "pre" }, new(false, false, null));

        Assert.Contains("(TS)</span>", BookPrinter.SongHtml(Item("TS"), false));
        Assert.Contains("(The source)</span>", BookPrinter.SongHtml(Item(null), false));
    }

    [Fact]
    public async Task OnlyThePeopleWithThePdfPermissionMayAsk()
    {
        using var factory = new ShelfApiFactory();
        using var client = factory.CreateClient();
        var person = Guid.NewGuid();
        var request = new BookRequest { Items = [new() { ChordsId = "x" }], Options = new() };

        await AssertErrorAsync(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/book", null, body: request)), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        await AssertErrorAsync(await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/book/pages", person, "", body: request)), HttpStatusCode.Forbidden, ErrorCodes.PermissionDenied);
        await AssertErrorAsync(
            await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/book", person, BuiltInPermissions.Pdf, body: request with { Items = [] })),
            HttpStatusCode.BadRequest,
            BookErrorCodes.BadItems);
        await AssertErrorAsync(
            await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/book", person, BuiltInPermissions.Pdf, body: request with { Items = [new() { ChordsId = "x", Transpose = 12 }] })),
            HttpStatusCode.BadRequest,
            BookErrorCodes.BadItem);
    }

    private static PreparedItem Prepared(string id, BookItemState state, int? number)
        => new(new() { ChordsId = id, Number = number }, state, 0, null, null, new(true, true, null));

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
        => await Browser.AssertError(response, status, code);
}
