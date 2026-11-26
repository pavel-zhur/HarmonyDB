using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Api;

namespace OneShelf.Collectives.Api.Functions.VInternal;

/// <summary>
/// The own chords by their authors: an internal client with the scope <see cref="CollectivesModule.InternalScope"/>
/// writes and reads a person's own chords here, naming the person by opaque names (<see cref="OwnChordsAuthor"/>); and
/// their source (the own chords' source of the contract, version 2) reads them all, with a function key
/// (<see cref="Items"/>). Own chords are the person's when any of the names is their author: of those written in the old
/// shelf, <c>telegram:</c> and its Telegram user's id (<see cref="Mapper.OldAuthor"/>); of the others
/// (<see cref="AuthoredCollective"/>), the name they were written by. The store compares names and asks no one what
/// one means. Nothing here touches the old shelf's songs.
/// <list type="bullet">
/// <item>A request that cannot be read is answered 400; own chords that do not exist, are deleted or are not the
/// author's are answered 404.</item>
/// <item>Only own chords written here are changed and deleted here (<see cref="OwnCollective.Editable"/>); those written in
/// the old shelf are read here and changed there: a change or a delete of them is answered 409.</item>
/// <item>A change adds a version, with a new code: a link to an older version opens nothing more.</item>
/// <item>A person's private own chords written here are removed for good, all at once, when their profile goes (<see cref="RemovePrivate"/>).</item>
/// </list>
/// </summary>
public class OwnFunctions(CollectivesDatabase database, UrlsManager urlsManager)
{
    [Function(CollectivesApiUrls.Items, AuthorizationLevel.Function, "post")]
    public async Task<IActionResult> Items(HttpRequest req, [FromBody] ItemsRequest request)
    {
        var ids = request.CollectiveIds?.Distinct().ToList();
        var old = ids == null ? await database.GetCollectives() : await database.GetCollectives(ids);
        var own = await database.GetAuthoredCollectives(ids);

        return new OkObjectResult(new ItemsResponse
        {
            Items = old.Where(x => x.LatestVisibility != CollectiveVisibility.Deleted).Select(x => x.ToOwn(urlsManager.Generate(x), false))
                .Concat(own.Where(x => x.LatestVisibility != CollectiveVisibility.Deleted).Select(x => x.ToOwn(Uri(x), false)))
                .ToList(),
        });
    }

    [Function(CollectivesApiUrls.OwnList, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> List(HttpRequest req, [FromBody] OwnListRequest request)
    {
        if (!IsComplete(request.Author)) return new BadRequestResult();

        var old = new List<Collective>();
        foreach (var telegramUserId in OldAuthors(request.Author))
            old.AddRange(await database.GetCollectives(telegramUserId));

        var own = await database.GetAuthoredCollectives(request.Author.Subjects);

        return new OkObjectResult(new OwnListResponse
        {
            Items = old.Where(x => x.LatestVisibility != CollectiveVisibility.Deleted).Select(x => x.ToOwn(urlsManager.Generate(x), false))
                .Concat(own.Where(x => x.LatestVisibility != CollectiveVisibility.Deleted).Select(x => x.ToOwn(Uri(x), false)))
                .OrderByDescending(x => x.UpdatedOn)
                .ToList(),
        });
    }

    [Function(CollectivesApiUrls.OwnGet, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Get(HttpRequest req, [FromBody] OwnGetRequest request)
    {
        if (!IsComplete(request.Author)) return new BadRequestResult();

        return await Find(request.Author, request.CollectiveId) switch
        {
            (Collective old, _) => new OkObjectResult(old.ToOwn(urlsManager.Generate(old), true)),
            (_, AuthoredCollective own) => new OkObjectResult(own.ToOwn(Uri(own), true)),
            _ => new NotFoundResult(),
        };
    }

    [Function(CollectivesApiUrls.OwnInsert, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Insert(HttpRequest req, [FromBody] OwnInsertRequest request)
    {
        if (!IsComplete(request.Author) || request.Collective == null!) return new BadRequestResult();

        var collective = new AuthoredCollective
        {
            Id = Guid.NewGuid(),
            Author = request.Author.Subjects[0],
            LatestVisibility = request.Collective.Visibility.ToDatabaseVisibility(),
            Versions = [request.Collective.ToDatabaseVersion(NewCode())],
        };

        if (!IsValid(collective)) return new BadRequestResult();

        await database.UpsertAuthoredCollective(collective);
        return new OkObjectResult(collective.ToOwn(Uri(collective), true));
    }

    [Function(CollectivesApiUrls.OwnUpdate, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Update(HttpRequest req, [FromBody] OwnUpdateRequest request)
    {
        if (!IsComplete(request.Author) || request.Collective == null!) return new BadRequestResult();

        switch (await Find(request.Author, request.CollectiveId))
        {
            case (Collective, _):
                return new ConflictResult();
            case (_, AuthoredCollective own):
            {
                var version = request.Collective.ToDatabaseVersion(NewCode());
                own.Versions.Add(version);
                own.LatestVisibility = version.Visibility;
                if (!IsValid(own)) return new BadRequestResult();

                await database.UpsertAuthoredCollective(own);
                return new OkObjectResult(own.ToOwn(Uri(own), true));
            }
            default:
                return new NotFoundResult();
        }
    }

    [Function(CollectivesApiUrls.OwnDelete, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Delete(HttpRequest req, [FromBody] OwnDeleteRequest request)
    {
        if (!IsComplete(request.Author)) return new BadRequestResult();

        switch (await Find(request.Author, request.CollectiveId))
        {
            case (Collective, _):
                return new ConflictResult();
            case (_, AuthoredCollective own):
                own.Versions.Add(Deleted(own.Versions.Last()));
                own.LatestVisibility = CollectiveVisibility.Deleted;
                await database.UpsertAuthoredCollective(own);
                return new NoContentResult();
            default:
                return new NotFoundResult();
        }
    }

    [Function(CollectivesApiUrls.OwnRemovePrivate, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> RemovePrivate(HttpRequest req, [FromBody] OwnRemovePrivateRequest request)
    {
        if (!IsComplete(request.Author)) return new BadRequestResult();

        var ids = (await database.GetAuthoredCollectives(request.Author.Subjects))
            .Where(x => x.Versions.LastOrDefault(v => v.Visibility != CollectiveVisibility.Deleted)?.Visibility is null or CollectiveVisibility.Private)
            .Select(x => x.Id)
            .ToList();

        return new OkObjectResult(new OwnRemovePrivateResponse { Removed = ids.Count == 0 ? 0 : await database.RemoveAuthoredCollectives(ids) });
    }

    /// <summary>
    /// The person's own chords of the id that are not deleted: those whose author is one of the person's names.
    /// </summary>
    private async Task<(Collective? old, AuthoredCollective? own)> Find(OwnChordsAuthor author, Guid collectiveId)
    {
        var old = await database.GetCollective(collectiveId);
        if (old != null)
            return old.LatestVisibility != CollectiveVisibility.Deleted && author.Subjects.Contains(Mapper.OldAuthor(old.CreatedByUserId)) ? (old, null) : (null, null);

        var own = await database.GetAuthoredCollective(collectiveId);
        return own != null && own.LatestVisibility != CollectiveVisibility.Deleted && author.Subjects.Contains(own.Author) ? (null, own) : (null, null);
    }

    /// <summary>
    /// The Telegram users among the person's names: the authors of the person's chords written in the old shelf.
    /// </summary>
    private static IEnumerable<long> OldAuthors(OwnChordsAuthor author)
        => author.Subjects.Select(x => x.StartsWith("telegram:", StringComparison.Ordinal) && long.TryParse(x["telegram:".Length..], out var id) && Mapper.OldAuthor(id) == x ? id : (long?)null)
            .OfType<long>()
            .Distinct();

    /// <summary>
    /// The code of a new version: six digits.
    /// </summary>
    private static int NewCode() => Random.Shared.Next(100000, 999999);

    private static Database.Models.Version Deleted(Database.Models.Version last) => last with
    {
        CreatedOn = DateTime.Now,
        Visibility = CollectiveVisibility.Deleted,
    };

    private Uri Uri(AuthoredCollective collective) => new(urlsManager.Generate(collective.Id, collective.Versions.Last().SearchTag, collective.Versions.Count));

    private static bool IsComplete(OwnChordsAuthor? author)
        => author is { Subjects: { Count: > 0 } subjects } && subjects.All(x => !string.IsNullOrWhiteSpace(x));

    /// <summary>
    /// The store's rules for own chords (authors and title trimmed, contents not empty): what breaks them is the caller's
    /// mistake.
    /// </summary>
    private static bool IsValid(object collective)
        => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(collective, new(collective), null, true);
}
