using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Cosmos;
using OneShelf.Common.Cosmos.Tools;
using OneShelf.Illustrations.Database.Models;
using OneShelf.Illustrations.Database.Options;

namespace OneShelf.Illustrations.Database
{
    public class IllustrationsCosmosDatabase : CosmosDatabase
    {
        private Container _illustrationPromptsContainer = null!;
        private Container _illustrationsContainer = null!;
        
        public IllustrationsCosmosDatabase(IOptions<IllustrationsCosmosDatabaseOptions> options,
            ILogger<IllustrationsCosmosDatabase> logger)
            : base(options.Value, logger)
        {
        }

        public async Task<List<IllustrationsPrompts>> GetPrompts(string? url = null)
        {
            await InitContainers();
            return await _illustrationPromptsContainer
                .GetItemLinqQueryable<IllustrationsPrompts>()
                .SelectSingle(x => url == null ? x : x.Where(x => x.Url == url))
                .ReadAll();
        }

        public async Task<List<string>> GetNonEmptyUrls()
        {
            await InitContainers();
            return await _illustrationPromptsContainer.GetItemLinqQueryable<IllustrationsPrompts>()
                .Select(x => x.Url)
                .Distinct()
                .ReadAll();
        }

        public async Task<List<Illustration>> GetIllustrationsWithoutPublicUrls()
        {
            await InitContainers();
            return await _illustrationsContainer.GetItemLinqQueryable<Illustration>()
                .Where(x => x.PublicUrl1024 == null)
                .ReadAll();
        }

        public async Task<List<IllustrationHeader>> GetIllustrationHeaders(string? url = null)
        {
            await InitContainers();
            return await _illustrationsContainer.GetItemLinqQueryable<Illustration>()
                .SelectSingle(x => url == null ? x : x.Where(x => x.Url == url))
                .Select(x => new IllustrationHeader
                {
                    CreatedOn = x.CreatedOn,
                    AttemptIndex = x.AttemptIndex,
                    Id = x.Id,
                    PhotoIndex = x.PhotoIndex,
                    Url = x.Url,
                    PublicUrl1024 = x.PublicUrl1024,
                    PublicUrl128 = x.PublicUrl128,
                    PublicUrl256 = x.PublicUrl256,
                    PublicUrl512 = x.PublicUrl512,
                    PromptsId = x.PromptsId,
                })
                .ReadAll();
        }

        public async Task<Illustration?> GetIllustration(Guid id)
        {
            await InitContainers();
            var all = await _illustrationsContainer.GetItemLinqQueryable<Illustration>()
                .Where(x => x.Id == id)
                .ReadAll();
            return all.SingleOrDefault();
        }

        public async Task AddPrompts(IllustrationsPrompts illustrationsPrompts)
        {
            await InitContainers();
            ValidateBeforeSaving(illustrationsPrompts);
            await _illustrationPromptsContainer.CreateItemAsync(illustrationsPrompts);
        }

        public async Task AddIllustration(Illustration illustration)
        {
            await InitContainers();
            ValidateBeforeSaving(illustration);
            await _illustrationsContainer.CreateItemAsync(illustration);
        }

        public async Task AddIllustrationPublicUrls(Guid id, Uri url1024, Uri url512, Uri url256, Uri url128)
        {
            await _illustrationsContainer.PatchItemAsync<Illustration>(id.ToString(), new(id.ToString()),
                new List<PatchOperation>
                {
                    PatchOperation.Set("/publicUrl1024", url1024),
                    PatchOperation.Set("/publicUrl512", url512),
                    PatchOperation.Set("/publicUrl256", url256),
                    PatchOperation.Set("/publicUrl128", url128),
                });
        }

        private async Task InitContainers()
        {
            if (_illustrationPromptsContainer != null && _illustrationsContainer != null) return;

            using var _ = await _lock.LockAsync();
            var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, 1000);
            _illustrationPromptsContainer = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new(nameof(IllustrationsPrompts), "/id")
            {
                IndexingPolicy =
                {
                    IncludedPaths =
                    {
                        new()
                        {
                            Path = "/url/?",
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
            _illustrationsContainer = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new(nameof(Illustration), "/id")
            {
                IndexingPolicy =
                {
                    IncludedPaths =
                    {
                        new()
                        {
                            Path = "/url/?",
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

        public async Task PublishCustomSystemMessage(Guid illustrationId, int specialSystemMessage, string? alterationKey)
        {
            var illustration = await GetIllustration(illustrationId);
            var promptsId = illustration!.PromptsId;

            var existingPrompts = await GetPrompts(illustration.Url);
            if (existingPrompts.Any(x => x.SpecialSystemMessage == specialSystemMessage && x.AlterationKey == alterationKey && x.Id != promptsId))
                throw new SupportedException("Such version already exists for that url.");

            var targetPrompt = existingPrompts.Single(x => x.Id == promptsId);
            if ((targetPrompt.SpecialSystemMessage.HasValue && targetPrompt.SpecialSystemMessage != specialSystemMessage) 
                || (targetPrompt.AlterationKey != null && targetPrompt.AlterationKey != alterationKey))
                throw new SupportedException("Another special system message or alteration key is already assigned to the corresponding prompt.");

            await _illustrationPromptsContainer.PatchItemAsync<IllustrationsPrompts>(promptsId.ToString(), new(promptsId.ToString()),
                new List<PatchOperation>
                {
                    PatchOperation.Set("/specialSystemMessage", specialSystemMessage),
                    PatchOperation.Set("/alterationKey", alterationKey),
                    PatchOperation.Remove("/customSystemMessage"),
                });
        }
    }
}
