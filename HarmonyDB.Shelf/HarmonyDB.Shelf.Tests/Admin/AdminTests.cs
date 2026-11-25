using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Api.Areas.Admin.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Admin;
using HarmonyDB.Shelf.Model.Auth;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Shelf.Tests.Admin;

/// <summary>
/// The admin site's internal API: who may call it, the permissions there are, setting a person's permissions.
/// </summary>
public sealed class AdminTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    private const string AdminKey = "the-admin-sites-key-of-the-tests-0123456789";

    private static readonly Dictionary<string, string?> AdminSettings = new()
    {
        ["InternalClients:1:Name"] = "admin",
        ["InternalClients:1:Key"] = AdminKey,
        ["InternalClients:1:Scopes:0"] = AdminScope,
        ["Permissions:Custom:0:Key"] = "custom-b",
        ["Permissions:Custom:0:Label"] = "Label B",
        ["Permissions:Custom:1:Key"] = "custom-a",
        ["Permissions:Custom:1:Label"] = "Label A",
    };

    private const string AdminScope = "shelf.admin";

    [Fact]
    public async Task OnlyAClientWithTheAdminScopeIsLetIn()
    {
        await using var api = new AuthApiFactory(null, "Production", AdminSettings);
        var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/internal/admin/permissions", null)).StatusCode);
        // a known client without the scope
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Get, "/internal/admin/permissions", AuthApiFactory.InternalKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Put, $"/internal/admin/people/{Guid.NewGuid()}/permissions", AuthApiFactory.InternalKey,
            new AdminSetPermissionsRequest { Permissions = [BuiltInPermissions.Pdf], Expected = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, HttpMethod.Get, "/internal/admin/permissions", "not-a-key-of-anyone-0123456789")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/internal/admin/permissions", AdminKey)).StatusCode);
    }

    [Fact]
    public async Task CustomPermissionsComeFromTheConfigurationOnly()
    {
        await using (var api = new AuthApiFactory(null, "Production", AdminSettings))
        {
            var catalog = await GetAsync<AdminPermissionCatalog>(api.CreateClient(), "/internal/admin/permissions");
            Assert.Equal(BuiltInPermissions.All, catalog.BuiltIn);
            Assert.Equal(["custom-b", "custom-a"], catalog.Custom.Select(x => x.Key));
            Assert.Equal(["Label B", "Label A"], catalog.Custom.Select(x => x.Label));
            Assert.Equal(["pdf"], catalog.NewAccountDefaults);
        }

        var withoutCustom = AdminSettings.Where(x => !x.Key.StartsWith("Permissions:Custom", StringComparison.Ordinal)).ToDictionary();
        await using (var api = new AuthApiFactory(null, "Production", withoutCustom))
        {
            var catalog = await GetAsync<AdminPermissionCatalog>(api.CreateClient(), "/internal/admin/permissions");
            Assert.Empty(catalog.Custom);
        }
    }

    [DatabaseFact]
    public async Task SettingPermissionsTakesEffectAtOnceAndIsAudited()
    {
        await using var api = new AuthApiFactory(database.ConnectionString, "Development", AdminSettings);
        var browser = api.NewBrowser();
        await browser.DevAsync("ticked");
        var accountId = (await browser.GetAsync<CurrentSession>("/api/auth/session")).AccountId!.Value;
        var admin = api.CreateClient();

        var person = await GetAsync<AdminPerson>(admin, $"/internal/admin/people/{accountId}");
        Assert.Equal(["pdf"], person.Permissions.Select(x => x.Permission));
        Assert.Equal("dev", Assert.Single(person.Summary.Logins).Provider);

        var response = await Send(admin, HttpMethod.Put, $"/internal/admin/people/{accountId}/permissions", AdminKey,
            new AdminSetPermissionsRequest { Permissions = [BuiltInPermissions.McpIndex, "custom-a"], Expected = ["pdf"] });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var change = (await response.Content.ReadFromJsonAsync<AdminPermissionChange>(Browser.Json))!;
        Assert.Equal(["custom-a", BuiltInPermissions.McpIndex], change.Added);
        Assert.Equal(["pdf"], change.Removed);

        // the person's next request goes by the new permissions (custom ones are not shown to the browser)
        var session = await browser.GetAsync<CurrentSession>("/api/auth/session");
        Assert.Equal([BuiltInPermissions.McpIndex], session.Permissions);

        await using var db = database.CreateContext();
        var entry = await db.AuditLog.SingleAsync(x => x.AccountId == accountId && x.Action == PeopleAdmin.PermissionsChangedAction);
        Assert.Equal("internal:admin", entry.Actor);
        var stored = await db.AccountPermissions.Where(x => x.AccountId == accountId && x.Permission == "custom-a").SingleAsync();
        Assert.Equal("admin", stored.GrantedBy);

        var changes = await GetAsync<List<AdminPermissionChange>>(admin, $"/internal/admin/permission-changes?accountId={accountId}");
        var listed = Assert.Single(changes);
        Assert.Equal(change.Added, listed.Added);
        Assert.Equal(change.Removed, listed.Removed);
        Assert.Equal("ticked", listed.DisplayName);

        // who holds a permission is found by it
        var holders = await GetAsync<AdminPeoplePage>(admin, "/internal/admin/people?permission=custom-a");
        Assert.Equal(accountId, Assert.Single(holders.People).Id);
        var withAny = await GetAsync<AdminPeoplePage>(admin, "/internal/admin/people?anyPermission=true&search=ticked");
        Assert.Equal(accountId, Assert.Single(withAny.People).Id);

        // the same set again changes nothing and writes nothing
        await Send(admin, HttpMethod.Put, $"/internal/admin/people/{accountId}/permissions", AdminKey,
            new AdminSetPermissionsRequest { Permissions = ["custom-a", BuiltInPermissions.McpIndex], Expected = ["custom-a", BuiltInPermissions.McpIndex] });
        Assert.Equal(1, await db.AuditLog.CountAsync(x => x.AccountId == accountId && x.Action == PeopleAdmin.PermissionsChangedAction));
    }

    [DatabaseFact]
    public async Task AnUnknownPermissionOrAStaleViewIsRefused()
    {
        await using var api = new AuthApiFactory(database.ConnectionString, "Development", AdminSettings);
        var browser = api.NewBrowser();
        await browser.DevAsync("refused");
        var accountId = (await browser.GetAsync<CurrentSession>("/api/auth/session")).AccountId!.Value;
        var admin = api.CreateClient();

        var unknown = await Send(admin, HttpMethod.Put, $"/internal/admin/people/{accountId}/permissions", AdminKey,
            new AdminSetPermissionsRequest { Permissions = ["pdf", "no-such-permission"], Expected = ["pdf"] });
        await Browser.AssertError(unknown, HttpStatusCode.BadRequest, AdminErrorCodes.UnknownPermission);

        var stale = await Send(admin, HttpMethod.Put, $"/internal/admin/people/{accountId}/permissions", AdminKey,
            new AdminSetPermissionsRequest { Permissions = [BuiltInPermissions.McpPdf], Expected = [] });
        await Browser.AssertError(stale, HttpStatusCode.Conflict, AdminErrorCodes.PermissionsChangedMeanwhile);

        await using var db = database.CreateContext();
        Assert.Equal(["pdf"], await db.AccountPermissions.Where(x => x.AccountId == accountId).Select(x => x.Permission).ToListAsync());
        Assert.False(await db.AuditLog.AnyAsync(x => x.AccountId == accountId && x.Action == PeopleAdmin.PermissionsChangedAction));
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? key, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (key != null)
            request.Headers.Add(InternalKeyDefaults.HeaderName, key);
        if (body != null)
            request.Content = JsonContent.Create(body, options: Browser.Json);
        return await client.SendAsync(request);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        var response = await Send(client, HttpMethod.Get, path, AdminKey);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Browser.Json))!;
    }
}
