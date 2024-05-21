using System.Net;
using HarmonyDB.Index.Api.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using OneShelf.Common.Songs;
using OneShelf.Frontend.Api.Model;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Instant;
using OneShelf.Frontend.Api.Services;
using VisitedChords = OneShelf.Common.Database.Songs.Model.VisitedChords;
using VisitedSearch = OneShelf.Common.Database.Songs.Model.VisitedSearch;

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class Apply : AuthorizationFunctionBase<ApplyRequest, ApplyResponse>
    {
        private readonly CollectionReaderV3 _collectionReaderV3;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FrontendOptions _frontendOptions;
        private readonly IndexApiClient _indexApiClient;
        private readonly SongsOperations _songsOperations;

        public Apply(ILoggerFactory loggerFactory, CollectionReaderV3 collectionReaderV3, IOptions<FrontendOptions> frontendOptions, IHttpClientFactory httpClientFactory, AuthorizationApiClient authorizationApiClient, IndexApiClient indexApiClient, SongsOperations songsOperations)
            : base(loggerFactory, authorizationApiClient)
        {
            _collectionReaderV3 = collectionReaderV3;
            _httpClientFactory = httpClientFactory;
            _indexApiClient = indexApiClient;
            _songsOperations = songsOperations;
            _frontendOptions = frontendOptions.Value;
        }

        [Function(ApiUrls.V3Apply)]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] ApplyRequest request) => await RunHandler(req, request);

        protected override async Task<ApplyResponse> Execute(HttpRequest httpRequest, ApplyRequest request)
        {
            var user = await _songsOperations.SongsDatabase.Users.SingleOrDefaultAsync(x => x.Id == request.Identity.Id);
            if (user == null)
            {
                throw new("You are not authorized.");
            }

            var anyChanges = false;

            try
            {
                await ApplyVisits(request, user);
            }
            catch (Exception e)
            {
                Logger.LogError("Could not apply visits.");
            }

            foreach (var updatedLike in request.UpdatedLikes.OrderBy(x => x.HappenedOn))
            {
                anyChanges = true;

                try
                {
                    await UpdateLike(updatedLike, user.Id);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Could not update the like.");
                }
            }

            foreach (var importedVersion in request.ImportedVersions.OrderBy(x => x.HappenedOn))
            {
                anyChanges = true;

                try
                {
                    await VersionImport(request.Identity, importedVersion, user.Id);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Could not import the version.");
                }
            }

            foreach (var removedVersion in request.RemovedVersions.OrderBy(x => x.HappenedOn))
            {
                anyChanges = true;

                try
                {
                    await VersionRemove(removedVersion, user.Id);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Could not remove the version.");
                }
            }

            if (anyChanges && !string.IsNullOrWhiteSpace(_frontendOptions.RegenerationKey) &&
                _frontendOptions.RegenerationUri != null)
            {
                try
                {
                    using var client = _httpClientFactory.CreateClient();
                    using var httpRequestMessage =
                        new HttpRequestMessage(HttpMethod.Get, _frontendOptions.RegenerationUri);
                    httpRequestMessage.Headers.Add("x-functions-key", _frontendOptions.RegenerationKey);
                    using var response = await client.SendAsync(httpRequestMessage);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Error queueing the regeneration.");
                }
            }

            var collection = await _collectionReaderV3.Read(TenantId, request.Identity);
            return new()
            {
                Collection = collection.Etag == request.Etag ? null : collection,
            };
        }

        private async Task VersionRemove(RemovedVersion request, long userId)
        {
            var version = await _songsOperations.SongsDatabase.Versions
                .Where(x => x.Song.TenantId == TenantId)
                .Include(x => x.Song)
                .SingleOrDefaultAsync(x => x.Id == request.VersionId);

            if (version == null)
            {
                Logger.LogWarning("Such chords didn't exist, {versionId}, {userId}", request.VersionId, userId);
                return;
            }

            if (version.Song.Status != SongStatus.Live) throw new ArgumentException($"The song status is not {SongStatus.Live} but {version.Song.Status}.", nameof(request.VersionId));
            
            if (version.UserId != userId)
            {
                Logger.LogWarning("wrong user deleting the version, {versionId}, {userId} != {uriUserId}", request.VersionId, userId, version.UserId);
                return;
            }

            if (version.PublishedSettings != null)
            {
                Logger.LogWarning("cannot delete the published version, {versionId}, {userId}, {publishedSettings}", request.VersionId, userId, version.PublishedSettings);
                return;
            }

            if (version.CollectiveType != null)
            {
                Logger.LogWarning("cannot delete the collective version, {versionId}, {userId}", request.VersionId, userId);
                return;
            }

            var likes = await _songsOperations.SongsDatabase.Likes.Where(x => x.VersionId == version.Id).ToListAsync();
            var songOwnLikes = (await _songsOperations.SongsDatabase.Songs
                .Where(x => x.TenantId == TenantId)
                .Where(x => x.Likes.Any(x => x.VersionId == version.Id))
                .SelectMany(x => x.Likes)
                .Where(x => x.VersionId == null)
                .Select(x => new
                {
                    x.SongId,
                    x.UserId,
                })
                .ToListAsync()).Select(x => (x.SongId, x.UserId)).ToHashSet();

            foreach (var like in likes)
            {
                if (songOwnLikes.Contains((like.SongId, like.UserId)))
                {
                    _songsOperations.SongsDatabase.Remove(like);
                }
                else
                {
                    like.VersionId = null;
                }
            }

            await _songsOperations.SongsDatabase.SaveChangesAsyncX();

            _songsOperations.SongsDatabase.Versions.Remove(version);

            await _songsOperations.SongsDatabase.SaveChangesAsyncX();
        }

        private async Task VersionImport(Identity identity, ImportedVersion request, long userId)
        {
            var header = await _indexApiClient.V1GetSearchHeader(identity, request.ExternalId);
            if (header.SourceUri == null) throw new("The source uri does not exist for these chords.");

            var (_, versionExisted, _) = await _songsOperations.VersionImport(TenantId, header.SourceUri, header.Artists ?? new List<string>(),
                header.Title, userId, request.Level, request.HappenedOn, request.LikeCategoryId, request.Transpose);

            if (versionExisted)
                throw new("These chords already exist.");
        }

        private async Task UpdateLike(UpdatedLike request, long userId)
        {
            if (request.Level is not (null or 0) && request.LikeCategoryId.HasValue)
            {
                Logger.LogWarning("Bad request: {request}, {userId}", request, userId);
                return;
            }

            LikeCategoryAccess? checkAccess = null;
            if (request.LikeCategoryId.HasValue)
            {
                var likeCategory = await _songsOperations.SongsDatabase.LikeCategories
                    .Where(x => x.TenantId == TenantId)
                    .SingleOrDefaultAsync(x => x.Id == request.LikeCategoryId.Value);
                if (likeCategory == null) return;
                if (likeCategory.UserId != userId)
                {
                    if (likeCategory.Access is LikeCategoryAccess.Private or LikeCategoryAccess.SharedView)
                    {
                        Logger.LogWarning("Bad request: {request}, {userId}", request, userId);
                        return;
                    }
                }

                checkAccess = likeCategory.Access;
            }

            var song = await _songsOperations.SongsDatabase.Songs
                .Where(x => x.TenantId == TenantId)
                .Include(x => x.Versions)
                .Include(x => x.Likes)
                .ThenInclude(x => x.Version)
                .SingleOrDefaultAsync(x => x.Id == request.SongId);
            if (song == null) throw new ArgumentException("The song does not exist.", nameof(request.SongId));
            if (song.Status != SongStatus.Live && !song.Versions.Any(x => x.CollectiveId.HasValue)) throw new ArgumentException($"The song status is not {SongStatus.Live} but {song.Status}.", nameof(request.SongId));

            var version = song.Versions.SingleOrDefault(x => x.Id == request.VersionId);

            if (version == null && request.VersionId.HasValue)
            {
                throw new($"Such chords don't exist, {request.SongId}, {request.VersionId}, {userId}");
            }

            var existingLike = song.Likes
                .Where(checkAccess == LikeCategoryAccess.SharedEditSingle ? x => true : x => x.UserId == userId)
                .SingleOrDefault(x => x.Version?.Id == request.VersionId && x.LikeCategoryId == request.LikeCategoryId);

            if (request.Level.HasValue)
            {
                if (existingLike == null)
                {
                    existingLike = new()
                    {
                        SongId = request.SongId,
                        CreatedOn = request.HappenedOn,
                        UserId = userId,
                        VersionId = version?.Id,
                        Version = version,
                        LikeCategoryId = request.LikeCategoryId,
                    };
                    _songsOperations.SongsDatabase.Likes.Add(existingLike);
                }

                existingLike.Level = Math.Max(version == null && !request.LikeCategoryId.HasValue ? (byte)1 : (byte)0, Math.Min(SongsConstants.MaxLevel, request.Level.Value));
                existingLike.Transpose = !request.Transpose.HasValue ? null : Math.Max(-7, Math.Min(7, request.Transpose.Value));
                await _songsOperations.SongsDatabase.SaveChangesAsyncX();
            }
            else
            {
                if (existingLike != null)
                {
                    _songsOperations.SongsDatabase.Likes.Remove(existingLike);
                    await _songsOperations.SongsDatabase.SaveChangesAsyncX();
                }
            }
        }

        private async Task ApplyVisits(ApplyRequest request, User user)
        {
            _songsOperations.SongsDatabase.VisitedChords.AddRange(request.VisitedChords.Select(x => new VisitedChords
            {
                ExternalId = x.ExternalId,
                SearchQuery = x.SearchQuery,
                ViewedOn = x.HappenedOn,
                UserId = user.Id,
                Transpose = x.Transpose,
                Uri = x.Uri,
                Artists = x.Artists,
                Title = x.Title,
                SongId = x.SongId,
                Source = x.Source,
            }));

            _songsOperations.SongsDatabase.VisitedSearches.AddRange(request.VisitedSearches.Select(x => new VisitedSearch
            {
                SearchedOn = x.HappenedOn,
                Query = x.Query,
                UserId = user.Id,
            }));

            await _songsOperations.SongsDatabase.SaveChangesAsyncX();
        }
    }
}
