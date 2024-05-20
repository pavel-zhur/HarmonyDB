using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.Ios;

namespace OneShelf.Telegram.Processor.Services;

public class RegenerationQueue
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RegenerationQueue> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly TelegramOptions _telegramOptions;

    public RegenerationQueue(IServiceScopeFactory serviceScopeFactory, ILogger<RegenerationQueue> logger, IHostApplicationLifetime hostApplicationLifetime, IOptions<TelegramOptions> telegramOptions)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _telegramOptions = telegramOptions.Value;
    }

    public void QueueDropLists()
    {
        var _ = ExecuteDifferentScope((r) => r.DropLists(), true);
    }

    public void QueueUpdateAll(bool isAdminDialog)
    {
        var _ = ExecuteDifferentScope((r) => r.UpdateAll(), isAdminDialog);
    }

    public async Task QueueUpdateAllSync()
    {
        await ExecuteDifferentScope((r) => r.UpdateAll(), false);
    }

    private async Task ExecuteDifferentScope(Func<Regeneration, Task> action, bool isAdminDialog)
    {
        var scope = _serviceScopeFactory.CreateScope();

        var ioFactory = scope.ServiceProvider.GetRequiredService<IoFactory>();

        if (isAdminDialog)
        {
            ioFactory.InitMonologue(_telegramOptions.AdminId, null);
        }
        else
        {
            ioFactory.InitDispose(_telegramOptions.AdminId, null);
        }

        await Go(action, _hostApplicationLifetime.ApplicationStopping, scope);
    }

    private async Task Go(Func<Regeneration, Task> action, CancellationToken cancellationToken, IServiceScope serviceScope)
    {
        try
        {
            var regeneration = serviceScope.ServiceProvider.GetRequiredService<Regeneration>();
            var io = serviceScope.ServiceProvider.GetRequiredService<Io>();
            var channelActions = serviceScope.ServiceProvider.GetRequiredService<ChannelActions>();

            await _semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                await action(regeneration);
                io.WriteLine("Regeneration finished successfully.");
            }
            catch (Exception e) when (e.HasInside<TaskCanceledException>())
            {
                io.WriteLine("Task canceled.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Queued process error.");
                io.WriteLine($"Queued process error: {e}");
            }
            finally
            {
                try
                {
                    if (io.SupportsFinish)
                    {
                        var finish = io.FinishAndGetFinish();
                        await channelActions.AdminPrivateMessageSafe(finish.ReplyMessageBody);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Finish go problem.");
                }
                finally
                {
                    try
                    {
                        _semaphoreSlim.Release();
                        serviceScope.Dispose();
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, "Disposal go problem.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Global go problem.");
        }
    }
}