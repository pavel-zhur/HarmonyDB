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
/// The own chords for the new shelf: it writes and reads its people's own chords here, as an internal client with the
/// scope <see cref="CollectivesModule.InternalScope"/>; and its source (the own chords' source of the contract, version 2)
/// reads them all, with a function key (<see cref="Items"/>). The new shelf's own chords belong to
/// its accounts (<see cref="AccountCollective"/>); those written in the old shelf belong to Telegram users and are the
/// shelf account's when it tells that it is that Telegram user. Nothing here touches the old shelf's songs.
/// <list type="bullet">
/// <item>A request that cannot be read is answered 400; own chords that do not exist, are deleted or are not the
/// author's are answered 404.</item>
/// <item>A change adds a version. A change of chords written in the old shelf keeps their search tag, so their address
/// in the old shelf keeps naming them.</item>
/// </list>
/// </summary>
public class OwnFunctions(CollectivesDatabase database, UrlsManager urlsManager)
{
    [Function(CollectivesApiUrls.Items, AuthorizationLevel.Function, "post")]
    public async Task<IActionResult> Items(HttpRequest req, [FromBody] ItemsRequest request)
    {
        var ids = request.CollectiveIds?.Distinct().ToList();
        var old = ids == null ? await database.GetCollectives() : await database.GetCollectives(ids);
        var own = await database.GetAccountCollectives(ids);

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
        foreach (var telegramUserId in request.Author.TelegramUserIds.Distinct())
            old.AddRange(await database.GetCollectives(telegramUserId));

        var own = await database.GetAccountCollectives(request.Author.AccountId);

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
            (_, AccountCollective own) => new OkObjectResult(own.ToOwn(Uri(own), true)),
            _ => new NotFoundResult(),
        };
    }

    [Function(CollectivesApiUrls.OwnInsert, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Insert(HttpRequest req, [FromBody] OwnInsertRequest request)
    {
        if (request.AccountId == Guid.Empty || request.Collective == null!) return new BadRequestResult();

        var collective = new AccountCollective
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            LatestVisibility = request.Collective.Visibility.ToDatabaseVisibility(),
            Versions = [request.Collective.ToDatabaseVersion(Random.Shared.Next(100000, 999999))],
        };

        if (!IsValid(collective)) return new BadRequestResult();

        await database.UpsertAccountCollective(collective);
        return new OkObjectResult(collective.ToOwn(Uri(collective), true));
    }

    [Function(CollectivesApiUrls.OwnUpdate, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Update(HttpRequest req, [FromBody] OwnUpdateRequest request)
    {
        if (!IsComplete(request.Author) || request.Collective == null!) return new BadRequestResult();

        switch (await Find(request.Author, request.CollectiveId))
        {
            case (Collective old, _):
            {
                var version = request.Collective.ToDatabaseVersion(old.Versions.Last().SearchTag);
                old.Versions.Add(version);
                old.LatestVisibility = version.Visibility;
                if (!IsValid(old)) return new BadRequestResult();

                await database.UpsertCollective(old);
                return new OkObjectResult(old.ToOwn(urlsManager.Generate(old), true));
            }
            case (_, AccountCollective own):
            {
                var version = request.Collective.ToDatabaseVersion(own.Versions.Last().SearchTag);
                own.Versions.Add(version);
                own.LatestVisibility = version.Visibility;
                if (!IsValid(own)) return new BadRequestResult();

                await database.UpsertAccountCollective(own);
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
            case (Collective old, _):
                old.Versions.Add(Deleted(old.Versions.Last()));
                old.LatestVisibility = CollectiveVisibility.Deleted;
                await database.UpsertCollective(old);
                return new NoContentResult();
            case (_, AccountCollective own):
                own.Versions.Add(Deleted(own.Versions.Last()));
                own.LatestVisibility = CollectiveVisibility.Deleted;
                await database.UpsertAccountCollective(own);
                return new NoContentResult();
            default:
                return new NotFoundResult();
        }
    }

    /// <summary>
    /// The own chords one account wrote become another's: the first was merged into the second. Done again, it moves
    /// nothing more.
    /// </summary>
    [Function(CollectivesApiUrls.OwnMove, AuthorizationLevel.Internal, "post", InternalScope = CollectivesModule.InternalScope)]
    public async Task<IActionResult> Move(HttpRequest req, [FromBody] OwnMoveRequest request)
    {
        if (request.FromAccountId == Guid.Empty || request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
            return new BadRequestResult();

        await database.MoveAccountCollectives(request.FromAccountId, request.ToAccountId);
        return new NoContentResult();
    }

    /// <summary>
    /// The author's own chords of the id that are not deleted: written in the old shelf by one of the author's Telegram
    /// users, or in the new one by the author's account.
    /// </summary>
    private async Task<(Collective? old, AccountCollective? own)> Find(OwnChordsAuthor author, Guid collectiveId)
    {
        var old = await database.GetCollective(collectiveId);
        if (old != null)
            return old.LatestVisibility != CollectiveVisibility.Deleted && author.TelegramUserIds.Contains(old.CreatedByUserId) ? (old, null) : (null, null);

        var own = await database.GetAccountCollective(collectiveId);
        return own != null && own.LatestVisibility != CollectiveVisibility.Deleted && own.AccountId == author.AccountId ? (null, own) : (null, null);
    }

    private static Database.Models.Version Deleted(Database.Models.Version last) => last with
    {
        CreatedOn = DateTime.Now,
        Visibility = CollectiveVisibility.Deleted,
    };

    private Uri Uri(AccountCollective collective) => new(urlsManager.Generate(collective.Id, collective.Versions.Last().SearchTag, collective.Versions.Count));

    private static bool IsComplete(OwnChordsAuthor? author) => author is { TelegramUserIds: not null } && author.AccountId != Guid.Empty;

    /// <summary>
    /// The store's rules for own chords (authors and title trimmed, contents not empty): what breaks them is the caller's
    /// mistake.
    /// </summary>
    private static bool IsValid(object collective)
        => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(collective, new(collective), null, true);
}
