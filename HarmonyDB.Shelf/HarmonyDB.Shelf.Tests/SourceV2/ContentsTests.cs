using System.Net;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.SourceV2;

/// <summary>
/// The content of many items at once, as the shelf asks the index for it.
/// </summary>
public sealed class ContentsTests
{
    private static readonly Caller Caller = new() { Subjects = [], Keys = ["k"] };

    [Fact]
    public async Task ManyContentsComeInOneCallInTheOrderAskedAndAnAnswerOutOfTheContractIsUnavailable()
    {
        var answerInOrder = true;
        var index = new FakeIndex
        {
            Answer = (operation, body) =>
            {
                if (operation != SourceV2Urls.V2GetContents)
                    return (HttpStatusCode.NotFound, null);

                var ids = body.GetProperty("ids").EnumerateArray().Select(x => x.GetString()!).ToList();
                if (!answerInOrder) ids.Reverse();
                return (HttpStatusCode.OK, new GetContentsResponse
                {
                    Items = ids.Select(id => id.StartsWith("test-", StringComparison.Ordinal)
                        ? new GetContentResponse { Id = id, State = ItemState.Open, Header = FakeIndex.Header(id, "A source"), Content = new() { Sheet = new() { Root = new NodeHtml { Name = "pre" } } } }
                        : new GetContentResponse { Id = id, State = ItemState.NotOpen }).ToList(),
                });
            },
        };
        using var api = index.Api(new ShelfApiFactory());
        await using var scope = api.Services.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<IndexClient>().Sources;

        var contents = await sources.GetContents(Caller, ["test-1", "closed-1", "test-2"], [ContentKind.Sheet]);
        Assert.Equal([("test-1", ItemState.Open), ("closed-1", ItemState.NotOpen), ("test-2", ItemState.Open)], contents.Items.Select(x => (x.Id, x.State)));
        Assert.NotNull(contents.Items[2].Content!.Sheet);
        var asked = Assert.Single(index.Requests);
        Assert.Equal(["k"], asked.Body.GetProperty("caller").GetProperty("keys").EnumerateArray().Select(x => x.GetString()));

        // answers not in the order asked are out of the contract: every item is unavailable
        answerInOrder = false;
        var broken = await sources.GetContents(Caller, ["test-1", "test-2"], [ContentKind.Sheet]);
        Assert.All(broken.Items, x => Assert.Equal(ItemState.Unavailable, x.State));
        Assert.Equal(["test-1", "test-2"], broken.Items.Select(x => x.Id));
    }
}
