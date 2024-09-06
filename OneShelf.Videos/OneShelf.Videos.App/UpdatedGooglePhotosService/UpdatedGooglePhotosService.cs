using System.Net;
using CasCap.Common.Extensions;
using CasCap.Exceptions;
using CasCap.Models;
using CasCap.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Videos.App.Models;

namespace OneShelf.Videos.App.UpdatedGooglePhotosService;

public class UpdatedGooglePhotosService : GooglePhotosService
{
    private const int DefaultBatchSizeMediaItems = 50;

    private readonly IOptions<VideosOptions> _videosOptions;
    private int _processed;

    public UpdatedGooglePhotosService(ILogger<GooglePhotosService> logger, IOptions<GooglePhotosOptions> options, HttpClient client, IOptions<VideosOptions> videosOptions)
        : base(logger, options, client)
    {
        _videosOptions = videosOptions;
    }

    [Obsolete]
    public void LoginWithOptions()
    {
        _client.DefaultRequestHeaders.Authorization = new(_videosOptions.Value.AuthorizationScheme!, _videosOptions.Value.AuthorizationParameter);
    }

    [Obsolete]
    public (string scheme, string? parameter) GetLogin()
    {
        return (
            _client.DefaultRequestHeaders.Authorization!.Scheme,
            _client.DefaultRequestHeaders.Authorization.Parameter);
    }

    public async Task AddMediaItemsToAlbumWithRetryAsync(string albumId, List<string> mediaItemIds)
    {
        var batches = mediaItemIds.Distinct().ToList().GetBatches(DefaultBatchSizeMediaItems);
        foreach (var batch in batches)
        {
            var req = new { mediaItemIds = batch.Value };
            var retry = 0;
            var delay = TimeSpan.FromSeconds(2);
            while (true)
            {
                var tpl = await PostJson<string, Error>(string.Format(RequestUris.POST_albums_batchAddMediaItems, albumId), req);
                if (tpl.error is not null)
                {
                    if (tpl.statusCode == HttpStatusCode.TooManyRequests && retry < 8)
                    {
                        retry++;
                        delay *= 2;
                        _logger.LogInformation("Retrying...");
                        await Task.Delay(delay);
                        continue;
                    }

                    throw new GooglePhotosException(tpl.error);
                }

                break;
            }
        }
    }

    public async Task<Dictionary<T, NewMediaItemResult>> UploadMultiple<T>(List<(T key, string filePath, string? description)> items, Func<Dictionary<T, NewMediaItemResult>, Task> newItemsAdded, Func<T, int, Task<string>>? transformFile = null, int threads = 10, int batchSize = 50)
        where T : struct
    {
        var uploadItems = new List<List<(UploadItem uploadItem, T key)>>
        {
            new()
        };

        using var handle = new EventWaitHandle(false, EventResetMode.AutoReset);
        var keepAdding = KeepAdding(uploadItems, handle, newItemsAdded);

        _processed = 0;

        var taken = 0;
        await Task.WhenAll(Enumerable.Range(0, threads).Select(i => Task.Run(async () =>
        {
            while (true)
            {
                while (uploadItems.Count - _processed > 2)
                    await Task.Delay(5000);

                (T key, string filePath, string? description) next;
                lock (items)
                {
                    if (taken == items.Count) break;
                    next = items[taken];
                    taken++;
                    _logger.LogInformation($"{i}: {taken - 1} / {items.Count}: uploading next...");
                }

                var filePath = next.filePath;
                if (transformFile != null)
                {
                    filePath = await transformFile(next.key, i);
                }

                var retry = 1;
                while (true)
                    try
                    {
                        var uploadToken = await UploadMediaAsync(filePath, GooglePhotosUploadMethod.Simple);
                        if (!string.IsNullOrWhiteSpace(uploadToken))
                        {
                            lock (uploadItems)
                            {
                                if (uploadItems[^1].Count == batchSize)
                                {
                                    uploadItems.Add(new());
                                    handle.Set();
                                }

                                uploadItems[^1].Add((new(uploadToken!, filePath, next.description), next.key));
                            }
                        }

                        break;
                    }
                    catch (GooglePhotosException) when (retry < 10)
                    {
                        Console.WriteLine("retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(2) * retry++);
                    }
            }

            _logger.LogInformation($"{i}: upload thread ended.");
        })));

        uploadItems.Add(new());
        uploadItems.Add(new());
        handle.Set();

        return await keepAdding;
    }

    private async Task<Dictionary<T, NewMediaItemResult>> KeepAdding<T>(List<List<(UploadItem uploadItem, T key)>> uploadItems, EventWaitHandle handle, Func<Dictionary<T, NewMediaItemResult>, Task> newItemsAdded)
        where T : notnull
    {
        await Task.Yield();
        _processed = 0;
        var result = new List<NewMediaItemResult>();
        while (true)
        {
            handle.WaitOne();
            List<List<(UploadItem uploadItem, T key)>> batches;
            lock (uploadItems)
            {
                batches = uploadItems.Skip(_processed).SkipLast(1).ToList();
                _processed += batches.Count;
            }

            foreach (var batch in batches)
            {
                if (batch.Any())
                {
                    _logger.LogInformation($"adding media items {batch.Count}..");
                    var newItems = (await AddMediaItemsAsync(batch.Select(x => x.uploadItem).ToList()))!.newMediaItemResults;
                    var newPairs = GetResultDictionary(uploadItems, newItems);
                    await newItemsAdded(newPairs);

                    result.AddRange(newItems);
                    _logger.LogInformation($"added media items {batch.Count}.");
                }
            }

            if (batches[^1].Count == 0) break;
        }

        return GetResultDictionary(uploadItems, result);
    }

    private static Dictionary<T, NewMediaItemResult> GetResultDictionary<T>(List<List<(UploadItem uploadItem, T key)>> uploadItems, List<NewMediaItemResult> items)
        where T : notnull
    {
        lock (uploadItems)
        {
            return uploadItems
                .SelectMany(x => x)
                .Join(items, x => x.uploadItem.uploadToken, x => x.uploadToken, (x, y) => (x, y))
                .ToDictionary(x => x.x.key, x => x.y);
        }
    }
}