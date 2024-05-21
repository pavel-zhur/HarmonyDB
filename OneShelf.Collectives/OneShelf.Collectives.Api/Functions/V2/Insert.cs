using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.V2;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;
using CollectiveVisibility = OneShelf.Collectives.Api.Model.V2.Sub.CollectiveVisibility;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Collectives.Api.Functions.V2
{
    public class Insert : AuthorizationFunctionBase<InsertRequest, InsertResponse>
    {
        private readonly CollectivesCosmosDatabase _collectivesCosmosDatabase;
        private readonly SongsOperations _songsOperations;
        private readonly UrlsManager _urlsManager;

        public Insert(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CollectivesCosmosDatabase collectivesCosmosDatabase, SongsOperations songsOperations, UrlsManager urlsManager) 
            : base(loggerFactory, authorizationApiClient)
        {
            _collectivesCosmosDatabase = collectivesCosmosDatabase;
            _songsOperations = songsOperations;
            _urlsManager = urlsManager;
        }

        [Function(CollectivesApiUrls.V2Insert)]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] InsertRequest request) => await RunHandler(req, request);

        protected override async Task<InsertResponse> Execute(HttpRequest httpRequest, InsertRequest request)
        {
            Version? version = null;
            if (request.DerivedFromVersionId.HasValue)
            {
                version = await _songsOperations.SongsDatabase.Versions
                    .Where(x => x.Song.TenantId == TenantId)
                    .Include(x => x.Song)
                    .SingleOrDefaultAsync(x => x.Id == request.DerivedFromVersionId);

                if (version == null)
                    throw new($"The hint version {request.DerivedFromVersionId} is not found.");

                if (version.Song.Status != SongStatus.Live)
                    throw new($"Unable to create a collective derived from a version of a song that is not live. Version id = {request.DerivedFromVersionId}.");
            }

            var databaseVersion = request.Collective.ToDatabaseVersion(Random.Shared.Next(100000, 999999));
            var collective = new Collective
            {
                Id = Guid.NewGuid(),
                CreatedByUserId = request.Identity.Id,
                LatestVisibility = request.Collective.Visibility.ToDatabaseVisibility(),
                Versions = new()
                {
                    databaseVersion,
                },
                DerivedFromUri = version?.Uri,
            };
            
            if (version != null)
            {
                _songsOperations.SongsDatabase.Versions.Add(new()
                {
                    SongId = version.SongId,
                    CollectiveId = collective.Id,
                    CollectiveSearchTag = databaseVersion.SearchTag,
                    CollectiveType = request.Collective.Visibility switch
                    {
                        CollectiveVisibility.Private => VersionCollectiveType.Private,
                        CollectiveVisibility.Club => VersionCollectiveType.Public,
                        CollectiveVisibility.Public => VersionCollectiveType.Public,
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    CreatedOn = DateTime.Now,
                    UserId = request.Identity.Id,
                    Uri = new(_urlsManager.Generate(collective.Id, databaseVersion.SearchTag, 1)),
                });
            }
            else
            {
                var newIndex = await _songsOperations.SongsDatabase.GetNextSongIndex(TenantId);
                var artists = await _songsOperations.FindOrCreateArtists(TenantId, request.Collective.Authors);

                _songsOperations.SongsDatabase.Songs.Add(new()
                {
                    TenantId = TenantId,
                    Artists = artists,
                    CreatedByUserId = request.Identity.Id,
                    CreatedOn = DateTime.Now,
                    Index = newIndex,
                    SourceUniqueIdentifier = collective.Id.ToString(),
                    Status = request.Collective.Visibility switch
                    {
                        CollectiveVisibility.Private => SongStatus.Draft,
                        CollectiveVisibility.Club => SongStatus.Live,
                        CollectiveVisibility.Public => SongStatus.Live,
                        _ => throw new ArgumentOutOfRangeException()
                    },
                    Title = request.Collective.Title.ToLowerInvariant().Trim(),
                    Versions = new List<Version>
                    {
                        new()
                        {
                            CollectiveId = collective.Id,
                            CollectiveSearchTag = databaseVersion.SearchTag,
                            CollectiveType = request.Collective.Visibility switch
                            {
                                CollectiveVisibility.Private => VersionCollectiveType.Private,
                                CollectiveVisibility.Club => VersionCollectiveType.Public,
                                CollectiveVisibility.Public => VersionCollectiveType.Public,
                                _ => throw new ArgumentOutOfRangeException()
                            },
                            CreatedOn = DateTime.Now,
                            UserId = request.Identity.Id,
                            Uri = new(_urlsManager.Generate(collective.Id, databaseVersion.SearchTag, 1)),
                        }
                    },
                });
            }

            await _collectivesCosmosDatabase.UpsertCollective(collective);
            await _songsOperations.SongsDatabase.SaveChangesAsyncX();

            return new()
            {
                Version = collective.ToModelVersion(_urlsManager.Generate(collective)),
            };
        }
    }
}
