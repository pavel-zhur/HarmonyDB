using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Songs.Hashing;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using LikeCategoryAccess = OneShelf.Common.Database.Songs.Model.Enums.LikeCategoryAccess;
using Version = OneShelf.Frontend.Api.Model.V3.Databasish.Version;
using VersionCollectiveType = OneShelf.Common.Database.Songs.Model.Enums.VersionCollectiveType;

namespace OneShelf.Frontend.Api.Services;

public class CollectionReaderV3
{
    private readonly ILogger<CollectionReaderV3> _logger;
    private readonly SongsDatabase _songsDatabase;
    private readonly SourceApiClient _sourceApiClient;
    private readonly SecurityContext _securityContext;

    public CollectionReaderV3(ILogger<CollectionReaderV3> logger, SongsDatabase songsDatabase, SourceApiClient sourceApiClient, SecurityContext securityContext)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
        _sourceApiClient = sourceApiClient;
        _securityContext = securityContext;
    }

    public async Task<Collection> Read(Identity identity)
    {
        var songs = await _songsDatabase.Songs
            .Where(x => x.TenantId == _securityContext.TenantId)
            .Include(x => x.Comments.Where(c => c.User.TenantId == _securityContext.TenantId))
            .Include(x => x.Likes.Where(c => c.User.TenantId == _securityContext.TenantId))
            .ThenInclude(x => x.Version)
            .Include(x => x.Versions)
            .ThenInclude(x => x.Comments)
            .Include(x => x.Artists)
            .ThenInclude(x => x.Synonyms)
            .ToListAsync();

        songs = songs
            .Where(x =>
            {
                if (x.Status is not (SongStatus.Draft or SongStatus.Live)) return false;

                var versions = x.Versions.Where(x => x.CollectiveType is (null or VersionCollectiveType.Public) || x.UserId == identity.Id).ToList();

                if (!versions.Any()) return false;

                x.Versions = versions;

                return true;
            })
            .ToList();

        var users = await _songsDatabase.Users
            .Where(x => x.TenantId == _securityContext.TenantId)
            .ToListAsync();

        var sourcesAndExternalIds = await _sourceApiClient.V1GetSourcesAndExternalIds(identity, songs
            .SelectMany(x => x.Versions)
            .Select(x => x.Uri));

        var likeCategories = await _songsDatabase.LikeCategories
            .Where(x => x.TenantId == _securityContext.TenantId)
            .Where(x => x.Access != LikeCategoryAccess.Private || x.UserId == identity.Id)
            .ToDictionaryAsync(x => x.Id);

        var collection = new Collection
        {
            Songs = songs
                .Select(x => new Song
                {
                    Id = x.Id,
                    TemplateRating = x.TemplateRating,
                    Index = x.Index,
                    Title = x.Title,
                    CreatedByUserId = x.CreatedByUserId,
                    CreatedOn = x.CreatedOn,
                    AdditionalKeywords = x.AdditionalKeywords,
                    Likes = x.Likes
                        .Where(x => !x.LikeCategoryId.HasValue || likeCategories.ContainsKey(x.LikeCategoryId.Value))
                        .Where(x => !x.LikeCategoryId.HasValue ||
                                    likeCategories[x.LikeCategoryId.Value].Access is not LikeCategoryAccess.Private ||
                                    likeCategories[x.LikeCategoryId.Value].UserId == identity.Id)
                        .OrderBy(x => x.Id)
                        .Select(x => new Like
                        {
                            VersionId = x.VersionId,
                            CreatedOn = x.CreatedOn,
                            Level = x.Level,
                            UserId = x.UserId,
                            Transpose = x.Transpose,
                            LikeCategoryId = x.LikeCategoryId,
                        })
                        .ToList(),
                    Artists = x.Artists.Select(x => x.Id).ToList(),
                    Versions = x.Versions
                        .OrderBy(x => x.Id)
                        .Select(x => new Version
                        {
                            Uri = x.Uri,
                            Id = x.Id,
                            UserId = x.UserId,
                            Source = sourcesAndExternalIds.GetValueOrDefault(x.Uri.ToString())?.Source,
                            ExternalId = sourcesAndExternalIds.GetValueOrDefault(x.Uri.ToString())?.ExternalId,
                            CreatedOn = x.CreatedOn,
                            Comments = x.Comments.Where(c => c.VersionId == x.Id).ToDictionary(x => x.UserId, x => x.Text),
                            CollectiveType = x.CollectiveType switch
                            {
                                VersionCollectiveType.Public => Model.V3.Databasish.VersionCollectiveType.Public,
                                VersionCollectiveType.Private => Model.V3.Databasish.VersionCollectiveType.Private,
                                null => null,
                                _ => throw new ArgumentOutOfRangeException(),
                            },
                            CollectiveId = x.CollectiveId,
                            CollectiveSearchTag = x.CollectiveSearchTag,
                        })
                        .ToList(),
                    Comments = x.Comments.Where(x => !x.VersionId.HasValue).ToDictionary(x => x.UserId, x => x.Text),
                })
                .OrderBy(x => x.Id)
                .ToList(),

            Artists = songs
                .SelectMany(x => x.Artists)
                .Distinct()
                .Select(x => new Artist
                {
                    Id = x.Id,
                    Name = x.Name,
                    Synonyms = x.Synonyms.Select(x => x.Title).ToList(),
                })
                .OrderBy(x => x.Id)
                .ToList(),

            Users = users
                .Select(x => new User
                {
                    Id = x.Id,
                    Title = x.Title,
                })
                .OrderBy(x => x.Id)
                .ToList(),

            LikeCategories = likeCategories
                .Values
                .Select(x => new LikeCategory
                {
                    CssColor = x.CssColor,
                    Id = x.Id,
                    UserId = x.UserId,
                    Name = x.Name,
                    Order = x.Order,
                    PrivateWeight = x.PrivateWeight,
                    CssIcon = x.CssIcon,
                    Access = x.Access switch
                    {
                        LikeCategoryAccess.Private => Model.V3.Databasish.LikeCategoryAccess.Private,
                        LikeCategoryAccess.SharedView => Model.V3.Databasish.LikeCategoryAccess.SharedView,
                        LikeCategoryAccess.SharedEditMulti => Model.V3.Databasish.LikeCategoryAccess.SharedEditMulti,
                        LikeCategoryAccess.SharedEditSingle => Model.V3.Databasish.LikeCategoryAccess.SharedEditSingle,
                        _ => throw new ArgumentOutOfRangeException(),
                    },

                })
                .OrderBy(x => x.Id)
                .ToList(),
        };

        collection.Etag = MyHashExtensions.CalculateHash(collection.DeepHash());

        return collection;
    }
}