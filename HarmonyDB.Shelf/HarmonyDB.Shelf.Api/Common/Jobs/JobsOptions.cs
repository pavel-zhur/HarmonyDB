using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Common.Jobs;

/// <summary>
/// The configuration section <c>Jobs</c>. Every setting has a default.
/// </summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>
    /// How many of the people's jobs one copy of the app runs at once.
    /// </summary>
    public int Concurrency { get; set; } = 4;

    /// <summary>
    /// How many of the shelf's own follow-ups (<see cref="JobLane.FollowUp"/>) one copy of the app runs at once, beside
    /// the people's jobs.
    /// </summary>
    public int FollowUpConcurrency { get; set; } = 1;

    /// <summary>
    /// How many jobs of one kind a person may have started and not yet ended: one person does not fill the queue. The
    /// shelf's follow-ups are not the person's and do not count.
    /// </summary>
    public int MaxActivePerPerson { get; set; } = 3;

    /// <summary>
    /// How long a running job is held by the copy of the app that runs it without word from it; renewed while it
    /// runs. When a copy stops without finishing a job, another (or the same after its restart) takes it again after
    /// this time.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How many times a job is started before an interrupted one is given up.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// How often the queue is looked at when nothing has been queued here.
    /// </summary>
    public TimeSpan Poll { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a finished job is in the person's list of recent ones.
    /// </summary>
    public TimeSpan RecentTime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// The most jobs the person's list gives.
    /// </summary>
    public int ListLimit { get; set; } = 50;

    /// <summary>
    /// How long the file a job made is kept.
    /// </summary>
    public TimeSpan FileLifetime { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// How long a finished job is kept at all.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);
}

internal sealed class JobsOptionsValidator : IValidateOptions<JobsOptions>
{
    public ValidateOptionsResult Validate(string? name, JobsOptions options)
    {
        var failures = new List<string>();
        var section = JobsOptions.SectionName;

        if (options.Concurrency < 1)
            failures.Add($"{section}:{nameof(JobsOptions.Concurrency)} is less than 1.");
        if (options.FollowUpConcurrency < 1)
            failures.Add($"{section}:{nameof(JobsOptions.FollowUpConcurrency)} is less than 1.");
        if (options.MaxActivePerPerson < 1)
            failures.Add($"{section}:{nameof(JobsOptions.MaxActivePerPerson)} is less than 1.");
        if (options.MaxAttempts < 1)
            failures.Add($"{section}:{nameof(JobsOptions.MaxAttempts)} is less than 1.");
        if (options.ListLimit < 1)
            failures.Add($"{section}:{nameof(JobsOptions.ListLimit)} is less than 1.");
        if (options.Lease <= TimeSpan.Zero || options.Poll <= TimeSpan.Zero || options.RecentTime <= TimeSpan.Zero
            || options.FileLifetime <= TimeSpan.Zero || options.Retention <= TimeSpan.Zero)
            failures.Add($"{section}: the times are not positive.");
        if (options.Retention < options.RecentTime || options.Retention < options.FileLifetime)
            failures.Add($"{section}:{nameof(JobsOptions.Retention)} is shorter than the recent time or the file lifetime.");

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
