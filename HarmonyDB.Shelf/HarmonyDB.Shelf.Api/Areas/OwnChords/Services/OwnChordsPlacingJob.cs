using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Model.OwnChords;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.OwnChords.Services;

/// <summary>
/// Places new own chords that were saved while the sources did not answer: asks the sources for their id; when they do
/// not answer, the job goes back to the queue to ask again after <see cref="OwnChordsPlacingOptions.Pause"/> (holding no
/// worker meanwhile), until <see cref="OwnChordsPlacingOptions.GiveUpAfter"/> from its start; then puts them where they
/// were to go. Stages: <c>placing</c>. Fails with <c>unavailable</c> when the sources never answered; the chords stay in
/// the person's list of own chords.
/// </summary>
public sealed class OwnChordsPlacingJob(OwnChordsService service, SourceCallers callers, IOptionsMonitor<OwnChordsPlacingOptions> options, TimeProvider time) : IJobHandler
{
    public async Task<JobCompletion> RunAsync(JobRun run, CancellationToken cancellationToken)
    {
        var placing = run.Input<OwnChordsService.Placing>();
        if (await service.ChordsIdAsync(await callers.ForAccountAsync(run.AccountId, placing.Keys, cancellationToken), placing.Uri, cancellationToken) is not { } chordsId)
        {
            var now = time.GetUtcNow();
            if (now - run.CreatedAt >= options.CurrentValue.GiveUpAfter)
                throw new JobFailedException(Model.ErrorCodes.Unavailable);

            return JobCompletion.Again(now + options.CurrentValue.Pause);
        }

        await run.ReportAsync("placing", cancellationToken: cancellationToken);
        await service.PlaceAsync(run.AccountId, chordsId, placing, cancellationToken);
        return new(new OwnChordsPlacingResult { ChordsId = chordsId });
    }
}

/// <summary>
/// The configuration section <c>OwnChords:Placing</c>.
/// </summary>
public sealed class OwnChordsPlacingOptions
{
    public const string SectionName = "OwnChords:Placing";

    /// <summary>
    /// The pause between two asks of the sources.
    /// </summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long after the saving the placing is given up.
    /// </summary>
    public TimeSpan GiveUpAfter { get; set; } = TimeSpan.FromMinutes(15);
}
