using System.Collections.Concurrent;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using OneShelf.Common.Cosmos;
using OneShelf.Common.Cosmos.Tools;
using OneShelf.Frontend.Database.Cosmos.Models;
using OneShelf.Frontend.Database.Cosmos.Options;

namespace OneShelf.Frontend.Database.Cosmos;

public class FrontendCosmosDatabase : CosmosDatabase
{
    private Container _pdfsContainer = null!;

    public FrontendCosmosDatabase(IOptions<FrontendCosmosDatabaseOptions> options, ILogger<FrontendCosmosDatabase> logger)
        : base(options.Value, logger)
    {
    }

    private async Task InitContainers()
    {
        if (_pdfsContainer != null) return;

        using var _ = await Lock.LockAsync();
        var databaseResponse = await Client.CreateDatabaseIfNotExistsAsync(Options.DatabaseName, 1000);
            
        _pdfsContainer = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new(nameof(Pdf), "/id")
        {
            IndexingPolicy =
            {
                IncludedPaths =
                {
                    new()
                    {
                        Path = "/version/?",
                    },
                    new()
                    {
                        Path = "/externalId/?",
                    },
                    new()
                    {
                        Path = "/createdOn/?",
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

    public async Task<Dictionary<string, Pdf>> GetPdfs(List<string> ids)
    {
        await InitContainers();

        return (await _pdfsContainer
            .GetItemLinqQueryable<Pdf>()
            .Where(x => ids.Contains(x.Id))
            .ReadAll()).ToDictionary(x => x.ExternalId);
    }

    public async Task<Dictionary<string, int>> GetPageCounts(List<string> ids)
    {
        await InitContainers();

        return (await _pdfsContainer
            .GetItemLinqQueryable<Pdf>()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.ExternalId,
                x.PageCount,
            })
            .ReadAll()).ToDictionary(x => x.ExternalId, x => x.PageCount);
    }

    public async Task AddOrUpdatePdfs(IReadOnlyCollection<Pdf> pdfs)
    {
        foreach (var pdf in pdfs)
        {
            ValidateBeforeSaving(pdf);
        }

        await InitContainers();
        var tasks = new List<Task>();

        while (true)
        {
            var retry = new ConcurrentBag<Pdf>();
            foreach (var pdf in pdfs)
            {
                tasks.Add(_pdfsContainer.UpsertItemAsync(pdf).ContinueWith(x =>
                {
                    if (!x.IsCompletedSuccessfully)
                    {
                        if (x.Exception?.InnerExceptions.All(x =>
                                x is CosmosException cosmosException &&
                                cosmosException.Message.Contains("(429)")) == true)
                        {
                            retry.Add(pdf);
                        }
                        else
                        {
                            throw new("Unexpected error.", x.Exception);
                        }
                    }
                }));
            }

            await tasks.WhenAll();
            if (retry.Any())
            {
                Console.WriteLine($"Retrying {retry.Count} pcs due to 429.");
                pdfs = retry;
            }
            else
            {
                break;
            }
        }
    }

    public async Task MigrateAsync()
    {
        await InitContainers();
    }
}