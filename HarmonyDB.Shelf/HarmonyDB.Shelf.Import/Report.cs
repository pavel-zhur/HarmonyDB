using HarmonyDB.Shelf.Import.Planning;

namespace HarmonyDB.Shelf.Import;

/// <summary>
/// What a run did, by kind of thing and outcome, and everything it could not map, with the reason.
/// </summary>
public sealed class Report
{
    public static class Outcomes
    {
        /// <summary>Made by this run.</summary>
        public const string Created = "created";

        /// <summary>Was already there, not made by the import (the person made it): taken as it is.</summary>
        public const string Found = "found";

        /// <summary>Made by an earlier run and changed since in the old shelf: updated.</summary>
        public const string Updated = "updated";

        /// <summary>Made by an earlier run, nothing to change.</summary>
        public const string Unchanged = "unchanged";

        /// <summary>Made by an earlier run and removed or changed since by a person in the new shelf: left as it is.</summary>
        public const string LeftByPerson = "left as the person has it";

        /// <summary>Made by an earlier run; its time set to the old shelf's (rows made before the times were carried).</summary>
        public const string Dated = "dated";

        /// <summary>Made by an earlier run, moved since: the variants of a song put back in the old order.</summary>
        public const string Reordered = "reordered";

        /// <summary>The person's account was merged into another: that account takes what comes.</summary>
        public const string FollowedMerge = "followed into the account it was merged into";

        /// <summary>Not written: what it belongs to is not there (removed by a person, or not made).</summary>
        public const string NoTarget = "skipped, its place is not there";
    }

    private readonly SortedDictionary<string, SortedDictionary<string, int>> _counts = new(StringComparer.Ordinal);

    public void Count(string kind, string outcome, int count = 1)
    {
        if (!_counts.TryGetValue(kind, out var outcomes))
            _counts[kind] = outcomes = new(StringComparer.Ordinal);
        outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + count;
    }

    public int Get(string kind, string outcome) => _counts.GetValueOrDefault(kind)?.GetValueOrDefault(outcome) ?? 0;

    public void Write(TextWriter output, ImportPlan plan, bool dryRun)
    {
        output.WriteLine(dryRun ? "DRY RUN: nothing is kept." : "RUN.");
        output.WriteLine();
        output.WriteLine("Planned:");
        output.WriteLine($"  people {plan.People.Count}, playlists {plan.Playlists.Count} ({string.Join(", ", plan.Playlists.GroupBy(PlaylistKind).Select(x => $"{x.Key} {x.Count()}"))}),");
        output.WriteLine($"  songs {plan.Playlists.Sum(x => x.Songs.Count)}, variants {plan.Playlists.Sum(x => x.Songs.Sum(s => s.Items.Count))}, members {plan.Playlists.Sum(x => x.Members.Count + 1)},");
        output.WriteLine($"  hearts {plan.Marks.Count} ({string.Join(", ", plan.Marks.GroupBy(x => x.Kind).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}),");
        output.WriteLine($"  keys {plan.Keys.Count} (mine {plan.Keys.Count(x => x.PlaylistKey == null)}, here {plan.Keys.Count(x => x.PlaylistKey != null)}), notes {plan.Notes.Count} (in playlists {plan.Notes.Count(x => x.SongKey != null)}, personal {plan.Notes.Count(x => x.SourceId != null)}),");
        output.WriteLine($"  recent {plan.Recent.Count}, main variants {plan.Preferences.Count}");
        output.WriteLine();
        output.WriteLine("Done:");
        foreach (var (kind, outcomes) in _counts)
            output.WriteLine($"  {kind}: {string.Join(", ", outcomes.Select(x => $"{x.Key} {x.Value}"))}");
        output.WriteLine();

        var pending = plan.Unmapped.Where(x => x.Pending).ToList();
        var notTaken = plan.Unmapped.Where(x => !x.Pending).ToList();
        output.WriteLine($"Waiting for their address to resolve (a later run takes them): {pending.Count}");
        foreach (var group in pending.GroupBy(x => x.Kind).OrderBy(x => x.Key))
            output.WriteLine($"  {group.Key}: {group.Count()}");
        output.WriteLine();
        output.WriteLine($"Not taken: {notTaken.Count}");
        foreach (var group in notTaken.GroupBy(x => (x.Kind, x.Reason)).OrderBy(x => x.Key.Kind).ThenBy(x => x.Key.Reason))
            output.WriteLine($"  {group.Key.Kind} — {group.Key.Reason}: {group.Count()}");
        output.WriteLine();
        output.WriteLine($"Taken as they are, to be looked at: {plan.Remarks.Count}");
        foreach (var item in plan.Remarks)
            output.WriteLine($"  {item.Kind}: {item.What} — {item.Reason}");
        output.WriteLine();
        output.WriteLine("Every item not taken or waiting:");
        foreach (var item in plan.Unmapped.OrderBy(x => x.Pending).ThenBy(x => x.Kind).ThenBy(x => x.Reason).ThenBy(x => x.TelegramId).ThenBy(x => x.What, StringComparer.Ordinal))
            output.WriteLine($"  [{(item.Pending ? "waiting" : "not taken")}] {item.Kind}{(item.TelegramId is { } person ? $" of {person}" : "")}: {item.What} — {item.Reason}");
    }

    private static string PlaylistKind(PlaylistPlan playlist) => playlist.Key.Split(':')[0];
}
