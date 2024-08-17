using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Processor.Services.PipelineHandlers;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using OneShelf.Telegram.Services;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services;

public class Pipeline
{
    private readonly PipelineMemory _pipelineMemory;
    private readonly ILogger<Pipeline> _logger;
    private readonly IReadOnlyList<PipelineHandler> _pipeline;
    private readonly RegenerationQueue _regenerationQueue;

    public Pipeline(
        PipelineMemory pipelineMemory,
        PinsRemover pinsRemover,
        ILogger<Pipeline> logger,
        LikesHandler likesHandler,
        UsersCollector usersCollector,
        InlineQueryHandler inlineQueryHandler,
        ChosenInlineResultCollector chosenInlineResultCollector,
        DialogHandler dialogHandler,
        RegenerationQueue regenerationQueue,
        OwnChatterHandler ownChatterHandler,
        PublicChatterHandler publicChatterHandler,
        PublicImportHandler publicImportHandler)
    {
        _pipelineMemory = pipelineMemory;
        _logger = logger;
        _regenerationQueue = regenerationQueue;
        _pipeline = new List<PipelineHandler>
        {
            usersCollector,
            likesHandler,
            inlineQueryHandler,
            chosenInlineResultCollector,
            pinsRemover,
            dialogHandler,
            ownChatterHandler,
            publicChatterHandler,
            publicImportHandler,
        };
    }

    /// <summary>
    /// Do not throw any exceptions, quickly process the update without waiting for all scheduled tasks to complete, schedule them, and dispose the scope when they are complete.
    /// </summary>
    public async Task<Task> ProcessSyncSafeAndDispose(Update update, AsyncServiceScope? scopeMustDispose)
    {
        List<Task> running = new();

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
                _logger.LogInformation("Unhandled {}", JsonSerializer.Serialize(update));
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Special catch error.");
        }

        return DisposeOnFinish(scopeMustDispose, running);
    }

    public async Task Regenerate()
    {
        await _regenerationQueue.QueueUpdateAllSync();
    }

    /// <remarks>It's async void on purpose. We don't await it.</remarks>
    private async Task DisposeOnFinish(AsyncServiceScope? scopeMustDispose, List<Task> running)
    {
        try
        {
            await Task.WhenAll(running);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in pipeline handlers continuations.");
        }
        finally
        {
            if (scopeMustDispose != null)
            {
                await scopeMustDispose.Value.DisposeAsync();
            }
        }
    }
}