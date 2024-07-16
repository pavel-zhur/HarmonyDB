using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Cosmos;
using OneShelf.Common.Cosmos.Tools;
using OneShelf.Collectives.Database.Models;
using OneShelf.Collectives.Database.Options;

namespace OneShelf.Collectives.Database
{
    public class CollectivesCosmosDatabase : CosmosDatabase
    {
        private Container _collectivesContainer = null!;
        private new readonly CollectivesCosmosDatabaseOptions _options;
        
        public CollectivesCosmosDatabase(IOptions<CollectivesCosmosDatabaseOptions> options,
            ILogger<CollectivesCosmosDatabase> logger)
            : base(options.Value, logger)
        {
            _options = options.Value;
        }

        public async Task<List<Collective>> GetCollectives(long? createdByUserId = null, CollectiveVisibility? visibility = null)
        {
            await InitContainers();
            return await _collectivesContainer.GetItemLinqQueryable<Collective>()
                .SelectSingle(x => createdByUserId == null ? x : x.Where(x => x.CreatedByUserId == createdByUserId))
                .SelectSingle(x => visibility == null ? x : x.Where(x => x.LatestVisibility == visibility))
                .ReadAll();
        }

        public async Task<Collective?> GetCollective(Guid id)
        {
            await InitContainers();
            var all = await _collectivesContainer.GetItemLinqQueryable<Collective>()
                .Where(x => x.Id == id)
                .ReadAll();
            return all.SingleOrDefault();
        }

        public async Task UpsertCollective(Collective collective)
        {
            await InitContainers();
            ValidateBeforeSaving(collective);
            await _collectivesContainer.UpsertItemAsync(collective);
        }

        private async Task InitContainers()
        {
            if (_collectivesContainer != null) return;

            using var _ = await Lock.LockAsync();
            var databaseResponse = await Client.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, 1000);

            _collectivesContainer = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new($"{nameof(Collective)}{_options.ContainerNamePostfix.SelectSingle(x => string.IsNullOrWhiteSpace(x) ? null : $"-{x}")}", "/createdByUserId")
            {
                IndexingPolicy =
                {
                    IncludedPaths =
                    {
                        new()
                        {
                            Path = "/createdByUserId/?",
                        },
                        new()
                        {
                            Path = "/latestVisibility/?",
                        },
                    },
                    ExcludedPaths =
                    {
                        new()
                        {
                            Path = "/*",
                        },
                    }
                }
            });
        }
    }
}
