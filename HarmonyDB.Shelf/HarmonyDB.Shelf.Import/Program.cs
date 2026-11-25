using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Import;
using HarmonyDB.Shelf.Import.Audit;
using HarmonyDB.Shelf.Import.Old;
using HarmonyDB.Shelf.Import.Planning;
using HarmonyDB.Shelf.Import.Resolving;
using HarmonyDB.Shelf.Import.Writing;
using HarmonyDB.Source.Api.Client.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// The one-off import of the old shelf's data into the shelf. Modes:
//   dry-run (default)  everything is done in a transaction that is rolled back; the report says what would be done
//   run                the same, kept
//   audit              compares, person by person, what each had in the old shelf with what they have in the shelf
//   second-pass        resolves the addresses the first pass left unresolved, one at a time, slowly, with the keys under
//                      which the sources may go out for them; stops cleanly (Ctrl+C, SIGTERM) and goes on where it stopped
var mode = args.FirstOrDefault(x => !x.StartsWith('-')) ?? "dry-run";
if (mode is not ("dry-run" or "run" or "audit" or "second-pass"))
{
    Console.Error.WriteLine("Usage: HarmonyDB.Shelf.Import [dry-run | run | audit | second-pass]");
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args.Where(x => x.StartsWith('-')).ToArray())
    .Build();

var options = configuration.GetSection(ImportOptions.SectionName).Get<ImportOptions>() ?? new();
options.Validate();
var relayOptions = configuration.GetSection(RelayOptions.SectionName).Get<RelayOptions>() ?? new();
var shelfConnection = Required("ConnectionStrings:Shelf");
var reader = new OldShelfReader(Required("ConnectionStrings:OldShelf"), Required("ConnectionStrings:OldOwnChords"));

using var loggerFactory = LoggerFactory.Create(x => x.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
using var sigterm = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, x => { x.Cancel = true; stopping.Cancel(); });
// only the second pass stops in the middle; the other modes run to their end, in their transaction
var cancellationToken = CancellationToken.None;
// the advisory locks of the import's runs (any two numbers of their own)
const long ImportLock = 7_351_201_001;
const long SecondPassLock = 7_351_201_002;

Console.WriteLine("Reading the old shelf …");
var old = await reader.ReadAsync(cancellationToken);
Console.WriteLine($"  tenants {old.Tenants.Count}, users {old.Users.Count}, songs {old.Songs.Count}, versions {old.Versions.Count}, likes {old.Likes.Count}, lists {old.Lists.Count}, comments {old.Comments.Count}, visits {old.Visits.Count}, own chords {old.OwnChords.Count}");

await using var db = new ShelfDbContext(new DbContextOptionsBuilder<ShelfDbContext>().UseShelfDatabase(shelfConnection).Options);

// one run at a time: the import's modes (the audit shares its lock, reading), and the second pass on its own
await using var lockConnection = new Npgsql.NpgsqlConnection(shelfConnection);
await lockConnection.OpenAsync(cancellationToken);
await using (var take = new Npgsql.NpgsqlCommand($"SELECT {(mode == "audit" ? "pg_try_advisory_lock_shared" : "pg_try_advisory_lock")}(@key)", lockConnection))
{
    take.Parameters.AddWithValue("key", mode == "second-pass" ? SecondPassLock : ImportLock);
    if (!(bool)(await take.ExecuteScalarAsync(cancellationToken))!)
    {
        Console.Error.WriteLine($"Another run of the import ({(mode == "second-pass" ? "second pass" : "import or audit")}) is going on; this one does not start.");
        return 3;
    }
}

if (mode == "audit")
{
    await using var readOnly = await db.Database.BeginTransactionAsync(cancellationToken);
    await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
    var auditor = new Auditor(old, options, db);
    var failures = await auditor.RunAsync(Console.Out, cancellationToken);
    await readOnly.RollbackAsync(cancellationToken);
    return failures == 0 ? 0 : 1;
}

if (mode == "second-pass")
    return await SecondPassAsync(stopping.Token);

var dryRun = mode == "dry-run";
var now = DateTimeOffset.UtcNow;
var report = new Report();
var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
var ledger = await Ledger.OpenAsync(db, cancellationToken);

// the people first: the own chords' source opens an author's private chords to their account, by its sign-in
var people = new Planner(old, options, Resolution.Empty).Plan().People;
var accounts = await new ShelfWriter(db, ledger, report, now).WritePeopleAsync(people, cancellationToken);
await ledger.SaveAsync(now, cancellationToken);
if (!dryRun)
{
    await transaction.CommitAsync(cancellationToken);
    await transaction.DisposeAsync();
    transaction = await db.Database.BeginTransactionAsync(cancellationToken);
}

Console.WriteLine("Resolving the addresses …");
var resolution = await ResolveAsync();

Console.WriteLine("Writing …");
var plan = new Planner(old, options, resolution).Plan();

await new ShelfWriter(db, ledger, report, now).WriteAsync(plan, accounts, cancellationToken);

if (dryRun)
    await transaction.RollbackAsync(cancellationToken);
else
    await transaction.CommitAsync(cancellationToken);
await transaction.DisposeAsync();

Console.WriteLine();
report.Write(Console.Out, plan, dryRun);
return 0;

async Task<Resolution> ResolveAsync()
{
    var (uris, ids) = Planner.Addresses(old, options);
    // the addresses resolved after this moment (by the second pass) are not in this run: the audit counts them as waiting
    ledger.Set(Ledger.LastRunKey, null, DateTimeOffset.UtcNow.ToString("O"));
    var known = await Ledger.ReadAddressesAsync(db, cancellationToken);
    using var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    var relay = new SourceV2Client(httpClient, new() { Name = "relay", Endpoint = relayOptions.Endpoint, Key = relayOptions.Key, Timeout = relayOptions.Timeout }, loggerFactory.CreateLogger<SourceV2Client>());
    var resolver = new AddressResolver(relay, relayOptions);

    // what resolved once stays resolved; the rest is asked again (a later pass)
    var addresses = uris.Where(x => known.GetValueOrDefault(("uri", x)) is { SourceId: not null }).ToDictionary(x => x, x => known[("uri", x)], StringComparer.Ordinal);
    var asked = await resolver.ResolveAsync(uris.Where(x => !addresses.ContainsKey(x)).ToList(), null, cancellationToken);

    // own chords not open to all are open to their author
    foreach (var author in old.Versions.Where(x => x.CollectiveId != null && asked.GetValueOrDefault(x.Uri) is { SourceId: null }).GroupBy(x => old.OwnChords.SingleOrDefault(c => c.Id == x.CollectiveId)?.AuthorUserId))
    {
        if (author.Key is { } person && accounts.TryGetValue(person, out var accountId))
            foreach (var (uri, answer) in await resolver.ResolveAsync(author.Select(x => x.Uri).Distinct().ToList(), accountId, cancellationToken))
                asked[uri] = answer;
    }

    foreach (var (uri, answer) in asked)
        addresses[uri] = answer;
    await Ledger.WriteAddressesAsync(db, "uri", asked, now, cancellationToken);

    var confirmed = ids.Where(x => known.GetValueOrDefault(("id", x)) is { SourceId: not null }).ToDictionary(x => x, x => known[("id", x)], StringComparer.Ordinal);
    var confirming = await resolver.ConfirmIdsAsync(ids.Where(x => !confirmed.ContainsKey(x)).ToList(), cancellationToken);
    foreach (var (id, answer) in confirming)
        confirmed[id] = answer;
    await Ledger.WriteAddressesAsync(db, "id", confirming, now, cancellationToken);

    Console.WriteLine($"  addresses {uris.Count}: resolved {addresses.Values.Count(x => x.SourceId != null)} ({asked.Count} asked now); "
                      + string.Join(", ", addresses.Values.Where(x => x.SourceId == null).GroupBy(x => x.State).Select(x => $"{x.Key} {x.Count()}")));
    Console.WriteLine($"  visits' ids {ids.Count}: open {confirmed.Values.Count(x => x.SourceId != null)} ({confirming.Count} asked now); "
                      + string.Join(", ", confirmed.Values.Where(x => x.SourceId == null).GroupBy(x => x.State).Select(x => $"{x.Key} {x.Count()}")));
    return new Resolution(addresses, confirmed);
}

// one address at a time, each answer kept at once: what is tried ("live") is not asked again, except what failed
async Task<int> SecondPassAsync(CancellationToken stop)
{
    if (relayOptions.SecondPassCallerKeys.Count == 0)
        throw new InvalidOperationException("Relay:SecondPassCallerKeys is not set.");

    await db.Database.OpenConnectionAsync(cancellationToken);
    var (uris, _) = Planner.Addresses(old, options);
    var ofVersions = old.Versions.Select(x => x.Uri).ToHashSet(StringComparer.Ordinal);
    // own chords resolve as their author, by the import itself: going out for them finds nothing
    var ownChords = old.Versions.Where(x => x.CollectiveId != null).Select(x => x.Uri).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var known = await Ledger.ReadAddressesAsync(db, cancellationToken);
    var tried = known.Where(x => x.Key.kind == "live" && x.Value.State != "unavailable").Select(x => x.Key.address).ToHashSet(StringComparer.Ordinal);
    var left = uris
        .Where(x => known.TryGetValue(("uri", x), out var answer) && answer.SourceId == null && !tried.Contains(x) && !ownChords.Contains(x))
        .OrderBy(x => ofVersions.Contains(x) ? 0 : 1)
        .ToList();
    Console.WriteLine($"Second pass: {left.Count} addresses to try ({tried.Count} tried before), one every {relayOptions.SecondPassPause}.");

    using var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    var relay = new SourceV2Client(httpClient, new() { Name = "relay", Endpoint = relayOptions.Endpoint, Key = relayOptions.Key, Timeout = relayOptions.Timeout }, loggerFactory.CreateLogger<SourceV2Client>());
    var resolver = new AddressResolver(relay, relayOptions);
    var done = 0;
    // a site that fails several times in a row is left alone for this start (its addresses stay for a later one)
    var failing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    try
    {
        foreach (var uri in left)
        {
            var site = Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;
            if (failing.GetValueOrDefault(site) >= relayOptions.SecondPassSiteFailures)
            {
                Console.WriteLine($"  skipped (its site keeps failing) {uri}");
                continue;
            }

            var answer = await resolver.ResolveLiveAsync(uri, stop);
            failing[site] = answer.State == "unavailable" ? failing.GetValueOrDefault(site) + 1 : 0;
            var at = DateTimeOffset.UtcNow;
            await Ledger.WriteAddressesAsync(db, "live", new Dictionary<string, ResolvedAddress> { [uri] = answer }, at, cancellationToken);
            if (answer.SourceId != null)
                await Ledger.WriteAddressesAsync(db, "uri", new Dictionary<string, ResolvedAddress> { [uri] = answer }, at, cancellationToken);
            Console.WriteLine($"  {++done}/{left.Count} {answer.State} {uri}");
            await Task.Delay(relayOptions.SecondPassPause, stop);
        }
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
        Console.WriteLine("Stopped; the next start goes on from here.");
    }
    catch (SourceUnavailableException)
    {
        Console.WriteLine("The relay does not answer: stopped; the next start goes on from here.");
        return 1;
    }

    var all = (await Ledger.ReadAddressesAsync(db, cancellationToken)).Where(x => x.Key.kind == "live").ToList();
    Console.WriteLine($"Second pass so far: tried {all.Count}; " + string.Join(", ", all.GroupBy(x => x.Value.State).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))
                      + ". Run the import again to take what resolved.");
    return 0;
}

string Required(string key)
    => configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"{key} is not set.");
