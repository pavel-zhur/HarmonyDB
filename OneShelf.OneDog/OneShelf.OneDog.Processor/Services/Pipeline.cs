using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneShelf.OneDog.Processor.Services.PipelineHandlers;
using OneShelf.OneDog.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services;

public class Pipeline
{
    private readonly PipelineMemory _pipelineMemory;
    private readonly ILogger<Pipeline> _logger;
    private readonly ScopeAwareness _scopeAwareness;
    private readonly IReadOnlyList<PipelineHandler> _pipeline;

    public Pipeline(
        PipelineMemory pipelineMemory,
        ILogger<Pipeline> logger,
        UsersCollector usersCollector,
        ChatsCollector chatsCollector,
        DialogHandler dialogHandler,
        OwnChatterHandler ownChatterHandler,
        ScopeAwareness scopeAwareness)
    {
        _pipelineMemory = pipelineMemory;
        _logger = logger;
        _scopeAwareness = scopeAwareness;
        _pipeline = new List<PipelineHandler>
        {
            usersCollector,
            chatsCollector,
            dialogHandler,
            ownChatterHandler,
        };
    }

    /// <summary>
    /// Do not throw any exceptions, quickly process the update without waiting for all scheduled tasks to complete, schedule them, and dispose the scope when they are complete.
    /// </summary>
    public async Task<Task> ProcessSyncSafeAndDispose(Update update, int domainId)
    {
        List<Task> running = new();

        await _scopeAwareness.Initialize(domainId);

        try
        {
            var anyHandled = false;
            foreach (var handler in _pipeline)
            {
                var (handled, tasks) = await handler.Handle(update);
                anyHandled |= handled;

                running.AddRange(tasks.Select(x => _pipelineMemory.ExecuteInOrder(x.queueKey, x.task)));

                if (handled)
                    break;
            }

            if (!anyHandled)
            {
                _logger.LogInformation("Unhandled {}", JsonConvert.SerializeObject(update, Formatting.Indented));
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Special catch error.");
        }

        return DisposeOnFinish(running);
    }

    /// <remarks>It's async void on purpose. We don't await it.</remarks>
    private async Task DisposeOnFinish(List<Task> running)
    {
        try
        {
            await Task.WhenAll(running);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in pipeline handlers continuations.");
        }
    }
}