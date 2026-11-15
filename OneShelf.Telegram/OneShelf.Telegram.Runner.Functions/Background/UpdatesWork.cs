using System.Text.Json;
using OneShelf.Common.Api.Background;
using OneShelf.Telegram.Services;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Runner.Functions.Background;

/// <summary>
/// Processes an update accepted by <see cref="Functions.BotFunctions.IncomingUpdate"/>.
/// </summary>
public class UpdatesWork : IBackgroundWork<string>
{
    private readonly Pipeline _pipeline;

    public UpdatesWork(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task Process(string item, CancellationToken cancellationToken)
    {
        var update = JsonSerializer.Deserialize<Update>(item) ?? throw new("Empty request body.");
        await await _pipeline.ProcessSyncSafeAndDispose(update, 0);
    }
}
