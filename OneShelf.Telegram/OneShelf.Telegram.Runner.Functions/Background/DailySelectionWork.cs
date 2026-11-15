using OneShelf.Common.Api.Background;
using OneShelf.Telegram.Processor.Services;

namespace OneShelf.Telegram.Runner.Functions.Background;

/// <summary>
/// Sends the daily selection to the users.
/// </summary>
public class DailySelectionWork : IScheduledWork
{
    private readonly DailySelection _dailySelection;

    public DailySelectionWork(DailySelection dailySelection)
    {
        _dailySelection = dailySelection;
    }

    public Task Run(CancellationToken cancellationToken) => _dailySelection.DailySelectionGo();
}
