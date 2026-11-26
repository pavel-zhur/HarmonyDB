using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Clients.OwnChords;
using HarmonyDB.Shelf.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HarmonyDB.Shelf.Tests.Auth;

/// <summary>
/// The tests that need a real PostgreSQL (sessions, sign-in flows, merges: transactions, partial unique indexes,
/// triggers) run when <c>SHELF_TEST_DATABASE</c> names a server (a connection string with a user that may create
/// databases); otherwise they are reported skipped. See README.md, "Testing".
/// </summary>
public static class TestDatabase
{
    public static string? Server => Environment.GetEnvironmentVariable("SHELF_TEST_DATABASE") is { Length: > 0 } value ? value : null;
}

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (TestDatabase.Server == null)
            Skip = "Needs a PostgreSQL: set SHELF_TEST_DATABASE (see README.md, Testing).";
    }
}

/// <summary>
/// A database of its own for a test class, migrated, dropped at the end; the API over it, in the Development
/// environment with the dev sign-in on and the local stack's plain http origin.
/// </summary>
public sealed class ShelfDatabaseFixture : IAsyncLifetime
{
    private readonly string _name = $"shelf_test_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = string.Empty;

    public AuthApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (TestDatabase.Server == null)
            return;

        await using (var connection = new NpgsqlConnection(TestDatabase.Server))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {_name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(TestDatabase.Server) { Database = _name }.ConnectionString;
        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        Api = new AuthApiFactory(ConnectionString);
    }

    public ShelfDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ShelfDbContext>().UseShelfDatabase(ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (TestDatabase.Server == null)
            return;

        await Api.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(TestDatabase.Server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {_name} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// The plain API (its own sign-in, no test scheme), over a database when one is given.
/// </summary>
public sealed class AuthApiFactory(string? connectionString, string environment = "Development", IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string InternalKey = "an-internal-key-of-the-tests-0123456789";

    /// <summary>
    /// The music service's purges of subjects.
    /// </summary>
    public FakePurges Purges { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(new Dictionary<string, string?>(TestClients.Settings)
        {
            ["ConnectionStrings:Shelf"] = connectionString ?? "Host=localhost;Database=shelf-tests",
            ["Shelf:Secrets:Key"] = Convert.ToBase64String(new byte[32]),
            ["Permissions:Defaults:NewAccount:0"] = "pdf",
            ["SignIn:Dev:Enabled"] = "true",
            // many sign-ins from one address in the tests; the limit itself has its own test
            ["SignIn:RateLimitPermits"] = "100000",
            // the local stack's plain http in Development; https elsewhere
            ["SignIn:PublicOrigin"] = environment == "Development" ? "http://localhost" : "https://shelf.example",
            // a known internal client of some other scope
            ["InternalClients:0:Name"] = "other",
            ["InternalClients:0:Key"] = InternalKey,
            ["InternalClients:0:Scopes:0"] = "shelf.other",
            ["Clients:OwnChords:Endpoint"] = NoOwnChords.Endpoint,
            ["Clients:OwnChords:Key"] = NoOwnChords.Key,
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<OwnChordsClient>().ConfigurePrimaryHttpMessageHandler(() => new NoOwnChords());
            services.AddHttpClient<AudioClient>().ConfigurePrimaryHttpMessageHandler(() => Purges);
        });
        if (settings != null)
            builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(settings));
    }

    public Browser NewBrowser() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
}
