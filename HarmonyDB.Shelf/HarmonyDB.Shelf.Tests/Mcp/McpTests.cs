using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Api.Areas.OAuth.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Auth;
using HarmonyDB.Shelf.Model.Mcp;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace HarmonyDB.Shelf.Tests.Mcp;

/// <summary>
/// The MCP server with its authorization server, over a real database: the OAuth flows (registration, the code with
/// PKCE, refresh rotation, reuse, revocation, confidential clients, one client's code or token presented by another),
/// the audience and expiry of tokens, the intersection of permissions read on every call, the tools listed and refused
/// by permission, refusals of other people's things through the tools, personal keys, the file links, the rate limit —
/// the MCP client being the official SDK's.
/// </summary>
public sealed class McpTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>, IAsyncLifetime
{
    private const string RedirectUri = "http://127.0.0.1:33418/callback";

    private McpApiFactory _api = null!;

    public Task InitializeAsync()
    {
        _api = new McpApiFactory(database.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public void OnlyPublicAddressesAreFetched()
    {
        foreach (var address in new[]
                 {
                     "127.0.0.1", "10.1.2.3", "172.16.0.1", "192.168.1.1", "169.254.169.254", "100.64.0.1", "0.0.0.0",
                     "224.0.0.1", "::1", "::", "fe80::1", "fc00::1", "fd12::1", "::ffff:10.0.0.1", "::ffff:127.0.0.1",
                     "2002:0a00:0001::1", "64:ff9b::a00:1", "64:ff9b:1::a00:1", "2001:0:4136:e378::1", "2001:db8::1",
                 })
            Assert.False(ClientMetadataFetcher.IsPublic(IPAddress.Parse(address)), address);

        foreach (var address in new[] { "8.8.8.8", "1.1.1.1", "2606:4700:4700::1111", "::ffff:8.8.8.8" })
            Assert.True(ClientMetadataFetcher.IsPublic(IPAddress.Parse(address)), address);
    }

    [Fact]
    public void AClientIdDocumentAddressHasTheDraftsForm()
    {
        Assert.True(ClientRegistry.IsMetadataDocumentId("https://assistant.example/oauth/client.json"));
        Assert.True(ClientRegistry.IsMetadataDocumentId("https://assistant.example:8443/client"));
        foreach (var id in new[]
                 {
                     "https://user@assistant.example/x.json", "https://user:pass@assistant.example/x.json", "https://assistant.example/x.json#a",
                     "https://assistant.example/a/../x.json", "https://assistant.example/./x.json", "https://assistant.example",
                     "https://assistant.example/", "http://assistant.example/x.json", "https://ASSISTANT.example/x.json", "c_abc",
                 })
            Assert.False(ClientRegistry.IsMetadataDocumentId(id), id);
    }

    [DatabaseFact]
    public async Task MetadataAndChallengeTellWhereToAuthorizeAndACookieNeverActs()
    {
        var http = _api.CreateClient();

        var unauthorized = await http.PostAsync("/mcp", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/mcp\"", unauthorized.Headers.WwwAuthenticate.ToString());

        var resource = await http.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp");
        Assert.Equal("http://localhost/mcp", resource.GetProperty("resource").GetString());
        Assert.Equal("http://localhost", resource.GetProperty("authorization_servers")[0].GetString());

        var server = await http.GetFromJsonAsync<JsonElement>("/.well-known/oauth-authorization-server");
        Assert.Equal("http://localhost", server.GetProperty("issuer").GetString());
        Assert.Equal("S256", server.GetProperty("code_challenge_methods_supported")[0].GetString());
        Assert.True(server.GetProperty("client_id_metadata_document_supported").GetBoolean());
        Assert.True(server.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());

        // a web page of another origin is refused
        var foreign = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        foreign.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(foreign)).StatusCode);

        // the person's own session (its cookie) does not open /mcp
        var (browser, _) = await PersonAsync("mcp-cookie", BuiltInPermissions.McpIndex);
        var withCookie = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        withCookie.Headers.Add("Cookie", string.Join("; ", browser.Cookies.Select(x => $"{x.Key}={x.Value}")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(withCookie)).StatusCode);
    }

    [DatabaseFact]
    public async Task TheCodeFlowGivesToolsByTheIntersectionReadOnEveryCall()
    {
        var (browser, accountId) = await PersonAsync("mcp-flow", BuiltInPermissions.McpIndex, BuiltInPermissions.McpLibraryRead, BuiltInPermissions.McpLibraryWrite);
        var clientId = await RegisterAsync(refresh: true);

        var (requestId, verifier) = await AuthorizeAsync(clientId, "mcp.index mcp.library.read mcp.pdf");
        var view = await browser.GetAsync<AuthorizationRequestView>($"/api/oauth/requests/{requestId}");
        Assert.Equal("Test assistant", view.Client.Name);
        Assert.True(view.Client.LocalOnly);
        Assert.Equal("127.0.0.1", view.Client.RedirectHost);
        Assert.Null(view.Client.ClientIdHost);
        Assert.Equal(["mcp.index", "mcp.library.read", "mcp.pdf"], view.Requested);
        Assert.Equal(["mcp.index", "mcp.library.read", "mcp.library.write"], view.Grantable);

        // more than the admin gave is refused
        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Post, $"/api/oauth/requests/{requestId}/approve", new ApproveAuthorizationRequest { Permissions = ["mcp.pdf"] }),
            HttpStatusCode.BadRequest, McpErrorCodes.NotGrantable);

        var tokens = await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index", "mcp.library.read"]);
        Assert.Equal("mcp.index mcp.library.read", tokens.GetProperty("scope").GetString());

        await using (var client = await ConnectAsync(tokens.GetProperty("access_token").GetString()!))
        {
            var tools = (await client.ListToolsAsync()).Select(x => x.Name).ToHashSet();
            Assert.Contains("search_chords", tools);
            Assert.Contains("list_playlists", tools);
            Assert.DoesNotContain("create_playlist", tools);
            Assert.DoesNotContain("make_book", tools);
            Assert.DoesNotContain("music_search", tools);

            Assert.NotEqual(true, (await client.CallToolAsync("list_playlists")).IsError);
            await Assert.ThrowsAnyAsync<Exception>(async () => await client.CallToolAsync("create_playlist", new Dictionary<string, object?> { ["title"] = "x" }));

            // the admin takes a permission away: at once, with the same token
            await using (var db = database.CreateContext())
                await db.AccountPermissions.Where(x => x.AccountId == accountId && x.Permission == BuiltInPermissions.McpLibraryRead).ExecuteDeleteAsync();
            tools = (await client.ListToolsAsync()).Select(x => x.Name).ToHashSet();
            Assert.Contains("search_chords", tools);
            Assert.DoesNotContain("list_playlists", tools);
        }

        var connected = Assert.Single(await browser.GetAsync<List<ConnectedClient>>("/api/mcp/clients"));
        Assert.Equal(["mcp.index", "mcp.library.read"], connected.Allowed);
        Assert.Equal(["mcp.index"], connected.InForce);
        Assert.Equal(["127.0.0.1"], connected.RedirectHosts);
        Assert.NotNull(connected.LastUsedAt);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, $"/api/mcp/clients/{connected.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(tokens.GetProperty("access_token").GetString()!)).StatusCode);
    }

    [DatabaseFact]
    public async Task AConsentIsThePersonsWhoOpenedItAndIsDecidedOnce()
    {
        var (browser, _) = await PersonAsync("mcp-once", BuiltInPermissions.McpIndex);
        var (other, _) = await PersonAsync("mcp-once-other");
        var clientId = await RegisterAsync(refresh: false);

        var (requestId, _) = await AuthorizeAsync(clientId, null);
        await browser.GetAsync<AuthorizationRequestView>($"/api/oauth/requests/{requestId}");

        // another person finds nothing, to look at or to decide
        await Browser.AssertError(await other.SendAsync(HttpMethod.Get, $"/api/oauth/requests/{requestId}"), HttpStatusCode.NotFound, McpErrorCodes.RequestInvalid);
        await Browser.AssertError(
            await other.SendAsync(HttpMethod.Post, $"/api/oauth/requests/{requestId}/approve", new ApproveAuthorizationRequest { Permissions = ["mcp.index"] }),
            HttpStatusCode.NotFound, McpErrorCodes.RequestInvalid);

        // decided once: a second approval, or a refusal after it, is refused
        await ApproveAsync(browser, requestId, ["mcp.index"]);
        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Post, $"/api/oauth/requests/{requestId}/approve", new ApproveAuthorizationRequest { Permissions = ["mcp.index"] }),
            HttpStatusCode.Conflict, McpErrorCodes.RequestDecided);
        await Browser.AssertError(await browser.SendAsync(HttpMethod.Post, $"/api/oauth/requests/{requestId}/deny"), HttpStatusCode.Conflict, McpErrorCodes.RequestDecided);

        // a request nobody opened yet is bound by the first decision too
        (requestId, _) = await AuthorizeAsync(clientId, null);
        Assert.Equal(HttpStatusCode.OK, (await other.SendAsync(HttpMethod.Post, $"/api/oauth/requests/{requestId}/deny")).StatusCode);
        await Browser.AssertError(await browser.SendAsync(HttpMethod.Get, $"/api/oauth/requests/{requestId}"), HttpStatusCode.NotFound, McpErrorCodes.RequestInvalid);
    }

    [DatabaseFact]
    public async Task AWiderConsentRevokesTheTokensGivenBeforeItANarrowerOneNarrowsThem()
    {
        var (browser, _) = await PersonAsync("mcp-reconsent", BuiltInPermissions.McpIndex, BuiltInPermissions.McpLibraryRead);
        var clientId = await RegisterAsync(refresh: false);

        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var first = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index"])).GetProperty("access_token").GetString()!;

        // wider: the earlier token, approved for less, goes
        (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var second = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index", "mcp.library.read"])).GetProperty("access_token").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(first)).StatusCode);

        // narrower: the live token keeps working, with less
        (requestId, verifier) = await AuthorizeAsync(clientId, null);
        await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index"]);
        await using var client = await ConnectAsync(second);
        Assert.DoesNotContain("list_playlists", (await client.ListToolsAsync()).Select(x => x.Name));
    }

    [DatabaseFact]
    public async Task HostsAreShownInTheirAsciiFormAndANameNotSeenAsItselfIsRefused()
    {
        // a look-alike: the first letter is Cyrillic
        const string lookAlike = "https://сlaude.ai/cb";
        var ascii = new System.Globalization.IdnMapping().GetAscii("сlaude.ai");
        Assert.StartsWith("xn--", ascii);

        var (browser, _) = await PersonAsync("mcp-hosts", BuiltInPermissions.McpIndex);
        var registered = await _api.CreateClient().PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
        {
            ["client_name"] = "Assistant",
            ["client_uri"] = "https://сlaude.ai/about",
            ["redirect_uris"] = new[] { lookAlike },
            ["token_endpoint_auth_method"] = "none",
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var clientId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client_id").GetString()!;

        var verifier = AuthTokens.NewToken();
        var authorize = await NoRedirects().GetAsync($"/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(lookAlike)}" +
                                                     $"&code_challenge={AuthTokens.PkceChallenge(verifier)}&code_challenge_method=S256");
        var requestId = HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["request"]!;
        var view = await browser.GetAsync<AuthorizationRequestView>($"/api/oauth/requests/{requestId}");
        Assert.Equal((ascii, $"https://{ascii}/about"), (view.Client.RedirectHost, view.Client.Uri));
        await ApproveAsync(browser, requestId, ["mcp.index"], redirectStart: lookAlike + "?");
        var connected = Assert.Single(await browser.GetAsync<List<ConnectedClient>>("/api/mcp/clients"));
        Assert.Equal([ascii], connected.RedirectHosts);
        Assert.Equal(($"https://{ascii}/about", "Assistant"), (connected.Uri, connected.Name));

        // a name kept from before names were bounded is shown as none: the client by its hosts
        await using (var db = database.CreateContext())
            await db.OAuthClients.Where(x => x.ClientId == clientId).ExecuteUpdateAsync(x => x.SetProperty(c => c.ClientName, new string('a', ClientRegistry.MaxName + 1)));
        Assert.Null(Assert.Single(await browser.GetAsync<List<ConnectedClient>>("/api/mcp/clients")).Name);

        Assert.Equal(ascii, ClientRegistry.IdHostOf(new OAuthClient { ClientId = "https://сlaude.ai/client.json", TokenEndpointAuthMethod = ClientRegistry.AuthNone }));

        // names: as given, or refused
        Assert.True(ClientRegistry.IsShowableName("Ассистент — mine"));
        foreach (var name in new[] { new string('a', ClientRegistry.MaxName + 1), "Good‮gnp.exe", "Two\nlines", "In​visible", "Bell\u0007" })
        {
            Assert.False(ClientRegistry.IsShowableName(name), name);
            var refused = await _api.CreateClient().PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
            {
                ["client_name"] = name,
                ["redirect_uris"] = new[] { "http://127.0.0.1/callback" },
                ["token_endpoint_auth_method"] = "none",
            });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("invalid_client_metadata", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        }
    }

    [DatabaseFact]
    public async Task AClientChangedInTheAppNarrowsAtOnceAndIsWidenedOnlyByAFreshSignInAndANewConsent()
    {
        var (browser, accountId) = await PersonAsync("mcp-change", BuiltInPermissions.McpIndex, BuiltInPermissions.McpLibraryRead);
        var clientId = await RegisterAsync(refresh: false);
        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var token = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index", "mcp.library.read"])).GetProperty("access_token").GetString()!;
        var connected = Assert.Single(await browser.GetAsync<List<ConnectedClient>>("/api/mcp/clients"));
        var path = $"/api/mcp/clients/{connected.Id}";

        // narrower: the live token keeps working, with less, at once
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Put, path, new UpdateMcpPermissionsRequest { Permissions = ["mcp.index"] })).StatusCode);
        await using (var client = await ConnectAsync(token))
            Assert.DoesNotContain("list_playlists", (await client.ListToolsAsync()).Select(x => x.Name));

        // the client's own token cannot change it
        var byToken = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(new UpdateMcpPermissionsRequest { Permissions = ["mcp.index", "mcp.library.read"] }) };
        byToken.Headers.Authorization = new("Bearer", token);
        Assert.Contains((await _api.CreateClient().SendAsync(byToken)).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Equal(["mcp.index"], Assert.Single(await browser.GetAsync<List<ConnectedClient>>("/api/mcp/clients")).Allowed);

        // wider, a while after signing in: refused, nothing changes
        await using (var db = database.CreateContext())
            await db.Sessions.Where(x => x.AccountId == accountId).ExecuteUpdateAsync(x => x.SetProperty(s => s.SignedInAt, DateTimeOffset.UtcNow.AddHours(-1)));
        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Put, path, new UpdateMcpPermissionsRequest { Permissions = ["mcp.index", "mcp.library.read"] }),
            HttpStatusCode.Forbidden, AuthErrorCodes.FreshSignInRequired);
        Assert.Equal(HttpStatusCode.OK, (await PostMcpAsync(token)).StatusCode);

        // wider, just signed in: the live token goes; the client is given more by a new consent
        await browser.DevAsync("mcp-change");
        Assert.Equal(HttpStatusCode.OK, (await browser.SendAsync(HttpMethod.Put, path, new UpdateMcpPermissionsRequest { Permissions = ["mcp.index", "mcp.library.read"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(token)).StatusCode);
    }

    [DatabaseFact]
    public async Task AWrongVerifierOrAReusedCodeGivesNothingAndAReusedCodeRevokesItsTokens()
    {
        var (browser, _) = await PersonAsync("mcp-codes", BuiltInPermissions.McpIndex);
        var clientId = await RegisterAsync(refresh: false);

        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var code = await ApproveAsync(browser, requestId, ["mcp.index"]);
        await AssertTokenErrorAsync(await RedeemAsync(clientId, code, new string('a', 43)), "invalid_grant");
        await AssertTokenErrorAsync(await RedeemAsync(clientId, code, verifier), "invalid_grant");

        (requestId, verifier) = await AuthorizeAsync(clientId, null);
        code = await ApproveAsync(browser, requestId, ["mcp.index"]);
        var first = await RedeemAsync(clientId, code, verifier);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstTokens = await first.Content.ReadFromJsonAsync<JsonElement>();
        var access = firstTokens.GetProperty("access_token").GetString()!;
        Assert.False(firstTokens.TryGetProperty("refresh_token", out _));
        Assert.Equal(HttpStatusCode.OK, (await PostMcpAsync(access)).StatusCode);

        await AssertTokenErrorAsync(await RedeemAsync(clientId, code, verifier), "invalid_grant");
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(access)).StatusCode);

        var noPkce = await NoRedirects().GetAsync($"/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&state=s1");
        Assert.StartsWith(RedirectUri + "?error=invalid_request", noPkce.Headers.Location!.ToString());

        var otherResource = await NoRedirects().GetAsync($"/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&code_challenge={AuthTokens.PkceChallenge(verifier)}&code_challenge_method=S256&resource={Uri.EscapeDataString("https://other.example/mcp")}");
        Assert.StartsWith(RedirectUri + "?error=invalid_target", otherResource.Headers.Location!.ToString());

        var wrongRedirect = await NoRedirects().GetAsync($"/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString("https://evil.example/cb")}&code_challenge={AuthTokens.PkceChallenge(verifier)}&code_challenge_method=S256");
        Assert.Equal("http://localhost/connect?error=redirect_uri_mismatch", wrongRedirect.Headers.Location!.ToString());
    }

    [DatabaseFact]
    public async Task OneClientsCodeOrTokenIsNothingInAnothersHands()
    {
        var (browser, _) = await PersonAsync("mcp-cross", BuiltInPermissions.McpIndex);
        var mine = await RegisterAsync(refresh: true);
        var other = await RegisterAsync(refresh: true);

        var (requestId, verifier) = await AuthorizeAsync(mine, null);
        var code = await ApproveAsync(browser, requestId, ["mcp.index"]);
        await AssertTokenErrorAsync(await RedeemAsync(other, code, verifier), "invalid_grant");

        // the code is still the right client's
        var redeemed = await RedeemAsync(mine, code, verifier);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var refresh = (await redeemed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refresh_token").GetString()!;
        await AssertTokenErrorAsync(await RefreshAsync(other, refresh), "invalid_grant");
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(mine, refresh)).StatusCode);
    }

    [DatabaseFact]
    public async Task AConfidentialClientProvesItselfAsItRegisteredAndRevocationEndsAFamily()
    {
        var (browser, _) = await PersonAsync("mcp-confidential", BuiltInPermissions.McpIndex);
        var registered = await (await _api.CreateClient().PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
        {
            ["redirect_uris"] = new[] { "http://127.0.0.1/callback" },
            ["grant_types"] = new[] { "authorization_code", "refresh_token" },
        })).Content.ReadFromJsonAsync<JsonElement>();
        var clientId = registered.GetProperty("client_id").GetString()!;
        var secret = registered.GetProperty("client_secret").GetString()!;
        Assert.Equal("client_secret_basic", registered.GetProperty("token_endpoint_auth_method").GetString());
        Assert.False(registered.TryGetProperty("client_name", out _));

        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        Assert.Null((await browser.GetAsync<AuthorizationRequestView>($"/api/oauth/requests/{requestId}")).Client.Name);
        var code = await ApproveAsync(browser, requestId, ["mcp.index"]);
        var form = new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = RedirectUri, ["code_verifier"] = verifier };

        // without its secret, or with a wrong one: not this client
        var anonymous = await PostFormAsync("/oauth/token", new(form) { ["client_id"] = clientId });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostFormAsync("/oauth/token", form, Basic(clientId, "wrong"))).StatusCode);

        var tokens = await (await PostFormAsync("/oauth/token", form, Basic(clientId, secret))).Content.ReadFromJsonAsync<JsonElement>();
        var access = tokens.GetProperty("access_token").GetString()!;
        var refresh = tokens.GetProperty("refresh_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await PostMcpAsync(access)).StatusCode);

        // revoking the refresh token ends its family; a token not the client's is answered the same, and stays
        Assert.Equal(HttpStatusCode.OK, (await PostFormAsync("/oauth/revoke", new() { ["token"] = refresh }, Basic(clientId, secret))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(access)).StatusCode);
        await AssertTokenErrorAsync(await PostFormAsync("/oauth/token", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }, Basic(clientId, secret)), "invalid_grant");
    }

    [DatabaseFact]
    public async Task RefreshRotatesAndAReusedRefreshTokenRevokesTheFamily()
    {
        var (browser, _) = await PersonAsync("mcp-refresh", BuiltInPermissions.McpIndex);
        var clientId = await RegisterAsync(refresh: true);
        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var tokens = await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index"]);
        var refresh1 = tokens.GetProperty("refresh_token").GetString()!;

        var second = await RefreshAsync(clientId, refresh1);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var rotated = await second.Content.ReadFromJsonAsync<JsonElement>();
        var access2 = rotated.GetProperty("access_token").GetString()!;
        var refresh2 = rotated.GetProperty("refresh_token").GetString()!;
        Assert.NotEqual(refresh1, refresh2);
        Assert.Equal(HttpStatusCode.OK, (await PostMcpAsync(access2)).StatusCode);

        await AssertTokenErrorAsync(await RefreshAsync(clientId, refresh1), "invalid_grant");
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(access2)).StatusCode);
        await AssertTokenErrorAsync(await RefreshAsync(clientId, refresh2), "invalid_grant");
    }

    [DatabaseFact]
    public async Task ATokenWorksOnlyForThisServerAndUntilItExpires()
    {
        var (browser, _) = await PersonAsync("mcp-audience", BuiltInPermissions.McpIndex);
        var clientId = await RegisterAsync(refresh: false);
        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var access = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index"])).GetProperty("access_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await PostMcpAsync(access)).StatusCode);

        await using var db = database.CreateContext();
        var hash = AuthTokens.Hash(access);

        await db.OAuthTokens.Where(x => x.TokenHash == hash).ExecuteUpdateAsync(x => x.SetProperty(t => t.Resource, "https://other.example/mcp"));
        var refused = await PostMcpAsync(access);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains("invalid_token", refused.Headers.WwwAuthenticate.ToString());

        await db.OAuthTokens.Where(x => x.TokenHash == hash).ExecuteUpdateAsync(x => x
            .SetProperty(t => t.Resource, "http://localhost/mcp")
            .SetProperty(t => t.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(access)).StatusCode);
    }

    [DatabaseFact]
    public async Task OtherPeoplesThingsAreRefusedThroughTheTools()
    {
        var (owner, _) = await PersonAsync("mcp-owner", BuiltInPermissions.McpLibraryRead, BuiltInPermissions.McpLibraryWrite);
        var (stranger, _) = await PersonAsync("mcp-stranger", BuiltInPermissions.McpLibraryRead, BuiltInPermissions.McpLibraryWrite);

        await using var ownerClient = await ConnectAsync(await KeyAsync(owner, ["mcp.library.read", "mcp.library.write"]));
        var created = Json(await ownerClient.CallToolAsync("create_playlist", new Dictionary<string, object?> { ["title"] = "Mine only" }));
        var playlistId = created.GetProperty("playlist").GetProperty("id").GetString()!;

        await using var strangerClient = await ConnectAsync(await KeyAsync(stranger, ["mcp.library.read", "mcp.library.write"]));
        foreach (var (tool, arguments) in new (string, Dictionary<string, object?>)[]
                 {
                     ("get_playlist", new() { ["playlistId"] = playlistId }),
                     ("list_playlist_songs", new() { ["playlistId"] = playlistId }),
                     ("update_playlist", new() { ["playlistId"] = playlistId, ["title"] = "Taken" }),
                     ("delete_playlist", new() { ["playlistId"] = playlistId }),
                     ("create_invitation", new() { ["playlistId"] = playlistId }),
                 })
        {
            var result = await strangerClient.CallToolAsync(tool, arguments);
            Assert.True(result.IsError, tool);
            Assert.Contains(Json(result).GetProperty("error").GetString(), new[] { "no_such_playlist", "members_only", "admins_only", "owner_only" });
        }

        var still = Json(await ownerClient.CallToolAsync("get_playlist", new Dictionary<string, object?> { ["playlistId"] = playlistId }));
        Assert.Equal("Mine only", still.GetProperty("playlist").GetProperty("title").GetString());
    }

    [DatabaseFact]
    public async Task APersonalKeyActsByWhatItWasGivenUntilRevoked()
    {
        var (browser, accountId) = await PersonAsync("mcp-key", BuiltInPermissions.McpIndex, BuiltInPermissions.McpPdf, BuiltInPermissions.McpYandexAnonymous);
        await Browser.AssertError(
            await browser.SendAsync(HttpMethod.Post, "/api/mcp/keys", new CreatePersonalKeyRequest { Name = "script", Permissions = ["mcp.library.write"] }),
            HttpStatusCode.BadRequest, McpErrorCodes.NotGrantable);

        var key = await KeyAsync(browser, ["mcp.pdf", "mcp.yandex.anonymous"], 30);
        Assert.StartsWith("shk_", key);

        await using (var client = await ConnectAsync(key))
        {
            var tools = (await client.ListToolsAsync()).Select(x => x.Name).ToHashSet();
            Assert.Contains("make_book", tools);
            Assert.Contains("get_job", tools);
            Assert.Contains("music_search", tools);
            Assert.DoesNotContain("music_my_likes", tools);
            Assert.DoesNotContain("search_chords", tools);

            // a service the key may not use is refused by the call's argument
            var youtube = await client.CallToolAsync("music_search", new Dictionary<string, object?> { ["service"] = "youtube", ["query"] = "anything" });
            Assert.True(youtube.IsError);
            Assert.Equal("permission_denied", Json(youtube).GetProperty("error").GetString());

            Assert.NotEqual(true, (await client.CallToolAsync("list_jobs")).IsError);
            var missing = await client.CallToolAsync("make_book", new Dictionary<string, object?> { ["caption"] = "x" });
            Assert.True(missing.IsError);
            Assert.Equal("bad_request", Json(missing).GetProperty("error").GetString());
            Assert.Contains("chordsIds", Json(missing).GetProperty("details").GetProperty("arguments").GetString());

            // a job's file: a link bound to the person and the job, short-lived; a changed link opens nothing
            var jobId = Guid.NewGuid();
            await using (var db = database.CreateContext())
            {
                db.Jobs.Add(new Job { Id = jobId, AccountId = accountId, Kind = "book", Input = "{}", State = JobState.Succeeded, CreatedAt = DateTimeOffset.UtcNow });
                db.JobOutputs.Add(new JobOutput { JobId = jobId, FileName = "book.pdf", ContentType = "application/pdf", Data = "%PDF-test"u8.ToArray(), CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
                await db.SaveChangesAsync();
            }

            var job = Json(await client.CallToolAsync("get_job", new Dictionary<string, object?> { ["jobId"] = jobId }));
            var fileUrl = new Uri(job.GetProperty("fileUrl").GetString()!);
            Assert.StartsWith("http://localhost/mcp/files/", fileUrl.ToString());
            var file = await _api.CreateClient().GetAsync(fileUrl.PathAndQuery);
            Assert.Equal("%PDF-test", await file.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Gone, (await _api.CreateClient().GetAsync(fileUrl.PathAndQuery[..^2] + "xx")).StatusCode);
        }

        var item = Assert.Single(await browser.GetAsync<List<PersonalKeyItem>>("/api/mcp/keys"));
        Assert.NotNull(item.LastUsedAt);
        Assert.NotNull(item.ExpiresAt);
        Assert.Equal(["mcp.pdf", "mcp.yandex.anonymous"], item.InForce);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, $"/api/mcp/keys/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostMcpAsync(key)).StatusCode);
    }

    [DatabaseFact]
    public async Task APersonsOwnMarksAreReadFromTheShelfAlone()
    {
        var index = new FakeIndex();
        await _api.DisposeAsync();
        _api = new McpApiFactory(database.ConnectionString, index: index);

        var (browser, accountId) = await PersonAsync("mcp-marks", BuiltInPermissions.McpLibraryRead);
        var chordsId = $"marked-{Guid.NewGuid():N}";
        await using (var db = database.CreateContext())
        {
            var chords = new ChordsItem { Id = Guid.NewGuid(), SourceId = chordsId, CreatedAt = DateTimeOffset.UtcNow };
            db.Chords.Add(chords);
            db.Marks.Add(new() { Id = Guid.NewGuid(), AccountId = accountId, ChordsId = chords.Id, Kind = "star", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var clientId = await RegisterAsync(refresh: false);
        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var access = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.library.read"])).GetProperty("access_token").GetString()!;
        await using var client = await ConnectAsync(access);
        var answer = Json(await client.CallToolAsync("get_my_marks_keys_notes", new Dictionary<string, object?> { ["chordsIds"] = new[] { chordsId } }));
        Assert.Equal("star", answer.GetProperty("marks")[0].GetProperty("kind").GetString());
        Assert.Empty(index.Requests);
    }

    [DatabaseFact]
    public async Task ASheetItsSourceDoesNotVouchForComesAsItsMessageAndPageWithoutItsText()
    {
        var index = new FakeIndex
        {
            Answer = (_, body) => (HttpStatusCode.OK, new GetContentResponse
            {
                Id = body.GetProperty("id").GetString()!,
                State = ItemState.Open,
                Header = FakeIndex.Header(body.GetProperty("id").GetString()!, "Source") with { Address = new("https://example.com/page") },
                Content = new() { Sheet = new() { Root = new NodeHtml { Name = "pre", Children = [new NodeChild(new NodeText("the sheet's words"))] }, UnstableMessage = "Its source's words" } },
            }),
        };
        await _api.DisposeAsync();
        _api = new McpApiFactory(database.ConnectionString, index: index);

        var (browser, _) = await PersonAsync("mcp-unstable", BuiltInPermissions.McpIndex);
        var clientId = await RegisterAsync(refresh: false);
        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var access = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index"])).GetProperty("access_token").GetString()!;

        await using var client = await ConnectAsync(access);
        var result = await client.CallToolAsync("get_chords", new Dictionary<string, object?> { ["chordsId"] = "u1" });
        var answer = Json(result);
        Assert.Equal("Its source's words", answer.GetProperty("unstableMessage").GetString());
        Assert.Equal("https://example.com/page", answer.GetProperty("address").GetString());
        Assert.False(answer.TryGetProperty("text", out _));
        Assert.DoesNotContain("the sheet's words", ((TextContentBlock)result.Content[0]).Text);
    }

    [DatabaseFact]
    public async Task AContentKindIsASummaryAndALinkThatOpensOnlyWhileTheClientMay()
    {
        // a stored analysis of ten minutes: 300 bars
        var timeline = new Timeline
        {
            Duration = 600,
            Keys = [new() { Key = "Am", Start = 0, End = 600 }],
            Bars = Enumerable.Range(0, 300).Select(i => new Bar
            {
                Start = i * 2,
                End = i * 2 + 2,
                Beats = [i * 2, i * 2 + 0.5, i * 2 + 1, i * 2 + 1.5],
                TimeSignature = "4/4",
                Tempo = new() { Bpm = 120, BeatUnit = new() { Denominator = 4 } },
            }).ToList(),
            Chords = [],
            Sections = [],
        };
        var index = new FakeIndex
        {
            Answer = (operation, body) => (HttpStatusCode.OK, new GetContentResponse
            {
                Id = body.GetProperty("id").GetString()!,
                State = ItemState.Open,
                Header = FakeIndex.Header(body.GetProperty("id").GetString()!, "Analyses") with { Kinds = [ContentKind.Sheet, ContentKind.Timeline] },
                Content = new() { Timeline = timeline },
            }),
        };
        await _api.DisposeAsync();
        _api = new McpApiFactory(database.ConnectionString, index: index);

        var (browser, accountId) = await PersonAsync("mcp-content", BuiltInPermissions.McpIndex);
        var clientId = await RegisterAsync(refresh: false);
        var (requestId, verifier) = await AuthorizeAsync(clientId, null);
        var access = (await ApproveAndRedeemAsync(browser, clientId, requestId, verifier, ["mcp.index"])).GetProperty("access_token").GetString()!;

        string url;
        await using (var client = await ConnectAsync(access))
        {
            var result = await client.CallToolAsync("get_chords", new Dictionary<string, object?> { ["chordsId"] = "a1", ["kind"] = "timeline" });
            var text = ((TextContentBlock)result.Content[0]).Text;
            Assert.True(Encoding.UTF8.GetByteCount(text) < 1024, text);
            var answer = Json(result);
            Assert.Equal(300, answer.GetProperty("summary").GetProperty("timeline").GetProperty("bars").GetInt32());
            Assert.Equal("Am", answer.GetProperty("summary").GetProperty("timeline").GetProperty("keys")[0].GetString());
            url = new Uri(answer.GetProperty("url").GetString()!).PathAndQuery;
        }

        // the link opens the full data
        var data = await _api.CreateClient().GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(300, data.GetProperty("bars").GetArrayLength());

        // a changed link opens nothing
        Assert.Equal(HttpStatusCode.Gone, (await _api.CreateClient().GetAsync(url[..^2] + "xx")).StatusCode);

        // the admin takes mcp.index away: the link stops; given back, it opens again
        await using (var db = database.CreateContext())
            await db.AccountPermissions.Where(x => x.AccountId == accountId && x.Permission == BuiltInPermissions.McpIndex).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.CreateClient().GetAsync(url)).StatusCode);
        await using (var db = database.CreateContext())
        {
            db.AccountPermissions.Add(new AccountPermission { AccountId = accountId, Permission = BuiltInPermissions.McpIndex, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await _api.CreateClient().GetAsync(url)).StatusCode);

        // the person disconnects the client: its link stops
        var connected = Assert.Single(await browser.GetAsync<List<ConnectedClient>>("/api/mcp/clients"));
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, $"/api/mcp/clients/{connected.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.CreateClient().GetAsync(url)).StatusCode);
    }

    [DatabaseFact]
    public async Task OneAddressOfManyPeopleIsNotLimitedButItsBadTokensAre()
    {
        // one address (as a hosted assistant's servers), a cap of 2 refused tokens and of 2 lookups at once
        await using var limited = new McpApiFactory(database.ConnectionString, new()
        {
            ["Mcp:RefusedPerAddress"] = "2",
            ["Mcp:LookupConcurrency"] = "2",
            ["Mcp:LookupQueue"] = "200",
        });
        var keys = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var browser = new Browser(limited.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
            await browser.DevAsync($"mcp-hosted-{i}");
            var accountId = (await browser.GetAsync<CurrentSession>("/api/auth/session")).AccountId!.Value;
            await using (var db = database.CreateContext())
            {
                db.AccountPermissions.Add(new AccountPermission { AccountId = accountId, Permission = BuiltInPermissions.McpIndex, GrantedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }

            keys.Add(await KeyAsync(browser, ["mcp.index"]));
        }

        // good tokens of different connections, all at once: all served (waiting their turn for the lookup)
        var good = await Task.WhenAll(Enumerable.Range(0, 60).Select(i => PostMcpAsync(keys[i % keys.Count], limited)));
        Assert.All(good, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        // bad tokens from the same address: refused, then answered 429 before any lookup
        var bad = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            bad.Add((await PostMcpAsync("not-a-token", limited)).StatusCode);
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], bad);
    }

    [DatabaseFact]
    public async Task CallsAreLimitedPerKey()
    {
        await using var limited = new McpApiFactory(database.ConnectionString, new() { ["Mcp:RateLimitPermits"] = "3", ["Mcp:RateLimitWindow"] = "00:10:00" });
        var browser = new Browser(limited.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
        await browser.DevAsync("mcp-limited");
        var accountId = (await browser.GetAsync<CurrentSession>("/api/auth/session")).AccountId!.Value;
        await using (var db = database.CreateContext())
        {
            db.AccountPermissions.Add(new AccountPermission { AccountId = accountId, Permission = BuiltInPermissions.McpIndex, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var key = await KeyAsync(browser, ["mcp.index"]);
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await PostMcpAsync(key, limited)).StatusCode);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [DatabaseFact]
    public async Task AClientDescribedByItsOwnDocumentIsReadAtTheAuthorization()
    {
        var (browser, _) = await PersonAsync("mcp-cimd", BuiltInPermissions.McpIndex);
        const string clientId = "https://assistant.example/oauth/client.json";
        _api.Documents[clientId] = new
        {
            client_id = clientId,
            client_name = "Described assistant",
            client_uri = "https://assistant.example",
            logo_uri = "https://assistant.example/logo.png",
            redirect_uris = new[] { "https://assistant.example/callback" },
            grant_types = new[] { "authorization_code", "refresh_token" },
            token_endpoint_auth_method = "none",
        };

        var verifier = AuthTokens.NewToken();
        var authorize = await NoRedirects().GetAsync($"/oauth/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString("https://assistant.example/callback")}&state=x&code_challenge={AuthTokens.PkceChallenge(verifier)}&code_challenge_method=S256&resource={Uri.EscapeDataString("http://localhost/mcp")}");
        var requestId = HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["request"]!;
        var view = await browser.GetAsync<AuthorizationRequestView>($"/api/oauth/requests/{requestId}");
        Assert.Equal(McpClientKind.MetadataDocument, view.Client.Kind);
        Assert.Equal("Described assistant", view.Client.Name);
        Assert.Equal("assistant.example", view.Client.ClientIdHost);
        Assert.Equal("assistant.example", view.Client.RedirectHost);
        Assert.False(view.Client.LocalOnly);

        var code = await ApproveAsync(browser, requestId, ["mcp.index"], "https://assistant.example/callback?");
        var redeemed = await PostFormAsync("/oauth/token", new()
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = "https://assistant.example/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp",
        });
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);

        // a document that does not name itself by its address is not a client; nor is an address of a forbidden form
        _api.Documents["https://assistant.example/other.json"] = new { client_id = clientId, client_name = "x", redirect_uris = new[] { "https://assistant.example/callback" } };
        _api.Documents["https://user@assistant.example/oauth/client.json"] = _api.Documents[clientId];
        foreach (var forged in new[] { "https://assistant.example/other.json", "https://user@assistant.example/oauth/client.json" })
        {
            var refused = await NoRedirects().GetAsync($"/oauth/authorize?response_type=code&client_id={Uri.EscapeDataString(forged)}&redirect_uri={Uri.EscapeDataString("https://assistant.example/callback")}&code_challenge={AuthTokens.PkceChallenge(verifier)}&code_challenge_method=S256");
            Assert.Equal("http://localhost/connect?error=unknown_mcp_client", refused.Headers.Location!.ToString());
        }
    }

    private HttpClient NoRedirects() => _api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(Browser Browser, Guid AccountId)> PersonAsync(string name, params string[] permissions)
    {
        var browser = new Browser(_api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
        await browser.DevAsync(name);
        var accountId = (await browser.GetAsync<CurrentSession>("/api/auth/session")).AccountId!.Value;
        await using var db = database.CreateContext();
        db.AccountPermissions.AddRange(permissions.Select(x => new AccountPermission { AccountId = accountId, Permission = x, GrantedAt = DateTimeOffset.UtcNow, GrantedBy = "tests" }));
        await db.SaveChangesAsync();
        return (browser, accountId);
    }

    private static async Task<string> KeyAsync(Browser browser, string[] permissions, int? expiresInDays = null)
    {
        var response = await browser.SendAsync(HttpMethod.Post, "/api/mcp/keys", new CreatePersonalKeyRequest { Name = "key", Permissions = permissions, ExpiresInDays = expiresInDays });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreatedPersonalKey>(Browser.Json))!;
        Assert.StartsWith(created.Item.Prefix, created.Key);
        return created.Key;
    }

    private async Task<string> RegisterAsync(bool refresh)
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
        {
            ["client_name"] = "Test assistant",
            ["redirect_uris"] = new[] { "http://127.0.0.1/callback" },
            ["grant_types"] = refresh ? new[] { "authorization_code", "refresh_token" } : new[] { "authorization_code" },
            ["token_endpoint_auth_method"] = "none",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(registered.TryGetProperty("client_secret", out _));
        return registered.GetProperty("client_id").GetString()!;
    }

    private async Task<(string RequestId, string Verifier)> AuthorizeAsync(string clientId, string? scope)
    {
        var verifier = AuthTokens.NewToken();
        var address = $"/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&state=st4te" +
                      $"&code_challenge={AuthTokens.PkceChallenge(verifier)}&code_challenge_method=S256&resource={Uri.EscapeDataString("http://localhost/mcp")}" +
                      (scope == null ? string.Empty : $"&scope={Uri.EscapeDataString(scope)}");
        var response = await NoRedirects().GetAsync(address);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith("http://localhost/connect?request=", location.ToString());
        return (HttpUtility.ParseQueryString(location.Query)["request"]!, verifier);
    }

    private static async Task<string> ApproveAsync(Browser browser, string requestId, string[] permissions, string redirectStart = RedirectUri + "?")
    {
        var response = await browser.SendAsync(HttpMethod.Post, $"/api/oauth/requests/{requestId}/approve", new ApproveAuthorizationRequest { Permissions = permissions });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redirect = new Uri((await response.Content.ReadFromJsonAsync<AuthorizationDecision>(Browser.Json))!.RedirectUrl);
        Assert.StartsWith(redirectStart, redirect.ToString());
        var query = HttpUtility.ParseQueryString(redirect.Query);
        Assert.Equal("http://localhost", query["iss"]);
        return query["code"]!;
    }

    private async Task<JsonElement> ApproveAndRedeemAsync(Browser browser, string clientId, string requestId, string verifier, string[] permissions)
    {
        var code = await ApproveAsync(browser, requestId, permissions);
        var response = await RedeemAsync(clientId, code, verifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> RedeemAsync(string clientId, string code, string verifier) => PostFormAsync("/oauth/token", new()
    {
        ["grant_type"] = "authorization_code",
        ["client_id"] = clientId,
        ["code"] = code,
        ["redirect_uri"] = RedirectUri,
        ["code_verifier"] = verifier,
        ["resource"] = "http://localhost/mcp",
    });

    private Task<HttpResponseMessage> RefreshAsync(string clientId, string refreshToken) => PostFormAsync("/oauth/token", new()
    {
        ["grant_type"] = "refresh_token",
        ["client_id"] = clientId,
        ["refresh_token"] = refreshToken,
        ["resource"] = "http://localhost/mcp",
    });

    private static AuthenticationHeaderValue Basic(string clientId, string secret)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(secret)}")));

    private Task<HttpResponseMessage> PostFormAsync(string path, Dictionary<string, string> form, AuthenticationHeaderValue? authorization = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = authorization;
        return _api.CreateClient().SendAsync(request);
    }

    private static async Task AssertTokenErrorAsync(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(error, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    private static JsonElement Json(CallToolResult result) => JsonDocument.Parse(((TextContentBlock)result.Content[0]).Text).RootElement;

    /// <summary>
    /// A bare MCP request with the token: 200 when the token is taken.
    /// </summary>
    private async Task<HttpResponseMessage> PostMcpAsync(string token, McpApiFactory? api = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return await (api ?? _api).CreateClient().SendAsync(request);
    }

    private Task<McpClient> ConnectAsync(string token)
        => McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri("http://localhost/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        }, _api.CreateClient()));

    /// <summary>
    /// The API over the test's database, with the clients' own description documents served from memory.
    /// </summary>
    private sealed class McpApiFactory(string connectionString, Dictionary<string, string?>? settings = null, FakeIndex? index = null) : WebApplicationFactory<Program>
    {
        public Dictionary<string, object> Documents { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(new Dictionary<string, string?>(TestClients.Settings)
            {
                ["ConnectionStrings:Shelf"] = connectionString,
                ["Shelf:Secrets:Key"] = Convert.ToBase64String(new byte[32]),
                ["SignIn:Dev:Enabled"] = "true",
                ["SignIn:RateLimitPermits"] = "100000",
                ["SignIn:PublicOrigin"] = "http://localhost",
                ["OAuth:RateLimitPermits"] = "100000",
                ["Mcp:RateLimitPermits"] = "100000",
                ["Mcp:RefusedPerAddress"] = "100000",
            }));
            if (index != null)
            {
                builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Clients:Index:Endpoint"] = FakeIndex.Endpoint,
                    ["Clients:Index:Key"] = FakeIndex.Key,
                }));
            }
            if (settings != null)
                builder.ConfigureAppConfiguration(x => x.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(ClientMetadataFetcher.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new DocumentsHandler(Documents));
                if (index != null)
                    services.AddHttpClient<IndexClient>().ConfigurePrimaryHttpMessageHandler(() => index);
            });
        }
    }

    private sealed class DocumentsHandler(Dictionary<string, object> documents) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(documents.TryGetValue(request.RequestUri!.OriginalString, out var document)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(document) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
