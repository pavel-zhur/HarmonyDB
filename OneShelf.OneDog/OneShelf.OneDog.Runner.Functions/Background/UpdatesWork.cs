using OneShelf.Common.Api.Background;
using OneShelf.Telegram.Services;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Runner.Functions.Background;

public class DomainUpdate
{
    public required Update Update { get; init; }

    public required int DomainId { get; init; }
}

/// <summary>
/// Processes an update accepted by <see cref="Functions.BotFunctions.IncomingUpdate"/>.
/// </summary>
public class UpdatesWork : IBackgroundWork<DomainUpdate>
{
    private readonly Pipeline _pipeline;

    public UpdatesWork(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task Process(DomainUpdate item, CancellationToken cancellationToken)
    {
        await await _pipeline.ProcessSyncSafeAndDispose(item.Update, item.DomainId);
    }
}
