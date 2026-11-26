using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Api.Foundation;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Tests.Api;

public sealed class FoundationTests(ShelfApiFactory factory) : IClassFixture<ShelfApiFactory>
{
    private static readonly Guid Account = Guid.NewGuid();

    [Fact]
    public async Task GuestHasTheGuestDefaults()
    {
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, "/api/test/pdf")).StatusCode);
        await AssertError(await Send(HttpMethod.Get, "/api/test/index"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
        await AssertError(await Send(HttpMethod.Post, "/api/test/write"), HttpStatusCode.Unauthorized, ErrorCodes.SignInRequired);
    }

    [Fact]
    public async Task PersonHasWhatTheAdminGave()
    {
        await AssertError(await Send(HttpMethod.Get, "/api/test/pdf", Account), HttpStatusCode.Forbidden, ErrorCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, "/api/test/index", Account, BuiltInPermissions.McpIndex)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/api/test/write", Account)).StatusCode);
    }

    [Fact]
    public void ClientGetsTheIntersection()
    {
        var identity = ShelfIdentity.Create(
            "client",
            Account,
            [],
            [BuiltInPermissions.McpIndex, BuiltInPermissions.McpPdf],
            [BuiltInPermissions.McpIndex, BuiltInPermissions.McpLibraryWrite]);

        Assert.Equal(
            [BuiltInPermissions.McpIndex],
            identity.FindAll(ShelfIdentity.PermissionClaimType).Select(x => x.Value));
    }

    [Fact]
    public async Task ClientActsByPermissionsOnly()
    {
        var index = new HttpRequestMessage(HttpMethod.Get, "/api/test/index");
        var write = new HttpRequestMessage(HttpMethod.Post, "/api/test/write");
        foreach (var request in new[] { index, write })
        {
            request.Headers.Add(ShelfApiFactory.AccountHeader, Account.ToString());
            request.Headers.Add(ShelfApiFactory.PermissionsHeader, BuiltInPermissions.McpIndex);
            request.Headers.Add(ShelfApiFactory.ClientAllowedHeader, BuiltInPermissions.McpIndex);
        }

        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().SendAsync(index)).StatusCode);
        await AssertError(await factory.CreateClient().SendAsync(write), HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task ErrorsAreCodes()
    {
        await AssertError(await Send(HttpMethod.Get, "/api/test/not-found"), HttpStatusCode.NotFound, "no_such_playlist");
        await AssertError(await Send(HttpMethod.Get, "/api/test/broken"), HttpStatusCode.InternalServerError, ErrorCodes.InternalError);
        await AssertError(await Send(HttpMethod.Get, "/api/nothing-here"), HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task PublicConfigurationHasNoPermissionsAndNoDevOutsideDevelopment()
    {
        var response = await factory.CreateClient().GetAsync("/api/app/configuration");
        var text = await response.Content.ReadAsStringAsync();
        var configuration = await response.Content.ReadFromJsonAsync<PublicConfiguration>(JsonOptions());

        Assert.Equal(["telegram"], configuration!.SignInProviders.Select(x => x.Name));
        Assert.Equal(["en", "ru"], configuration.Locales);
        Assert.DoesNotContain("custom.one", text);
        Assert.DoesNotContain("Custom one", text);
    }

    [Fact]
    public async Task TheShowcaseIsTheConfiguredPlaylistOrNone()
    {
        var none = await factory.CreateClient().GetFromJsonAsync<PublicConfiguration>("/api/app/configuration", JsonOptions());
        Assert.Null(none!.ShowcasePlaylistId);
        Assert.Equal("https://site.example", none.PublicPageOrigin);

        var id = Guid.NewGuid();
        using var configured = new ShelfApiFactory();
        configured.Settings["App:ShowcasePlaylistId"] = id.ToString();
        configured.Settings["PublicPage:Origin"] = "https://other.example/";
        var both = await configured.CreateClient().GetFromJsonAsync<PublicConfiguration>("/api/app/configuration", JsonOptions());
        Assert.Equal((id, "https://other.example"), (both!.ShowcasePlaylistId, both.PublicPageOrigin));

        using var bad = new ShelfApiFactory();
        bad.Settings["App:ShowcasePlaylistId"] = "not-a-playlist";
        Assert.Throws<OptionsValidationException>(() => bad.CreateClient());
    }

    [Fact]
    public void InternalEndpointsAreNotInTheBrowserDocument()
    {
        var paths = factory.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>()
            .GetSwagger(HarmonyDB.Shelf.Api.Foundation.OpenApi.OpenApiExtensions.DocumentName).Paths.Keys;

        Assert.Contains("/api/test/pdf", paths);
        Assert.DoesNotContain("/api/test/internal", paths);
        Assert.DoesNotContain("/internal/test", paths);
    }

    [Theory]
    [InlineData("ConnectionStrings:Shelf", "")]
    [InlineData("Permissions:Custom:0:Key", "pdf")]
    [InlineData("Permissions:Defaults:NewAccount:0", "unknown.permission")]
    public void BadPermissionsAreRefused(string key, string value)
    {
        using var bad = new ShelfApiFactory();
        bad.Settings[key] = value;
        Assert.Throws<OptionsValidationException>(() => bad.CreateClient());
    }

    [Theory]
    [InlineData("Clients:Audio:BaseUrl")]
    [InlineData("Clients:Index:Endpoint")]
    [InlineData("Clients:OwnChords:Endpoint")]
    [InlineData("Clients:OwnChords:PreviewEndpoint")]
    [InlineData("Clients:Illustrations:Endpoint")]
    [InlineData("Clients:Pdfs:Endpoint")]
    [InlineData("Media:SigningKey")]
    [InlineData("PublicPage:Origin")]
    public void WithoutWhatItNeedsTheAppDoesNotStart(string key)
    {
        using var bad = new ShelfApiFactory();
        Assert.True(bad.Settings.Remove(key));
        Assert.Throws<OptionsValidationException>(() => bad.CreateClient());
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, Guid? account = null, params string[] permissions)
    {
        var request = new HttpRequestMessage(method, path);
        if (account != null)
        {
            request.Headers.Add(ShelfApiFactory.AccountHeader, account.ToString());
            request.Headers.Add(ShelfApiFactory.PermissionsHeader, string.Join(',', permissions));
        }

        return factory.CreateClient().SendAsync(request);
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions());
        Assert.Equal(code, error!.Code);
    }

    private static System.Text.Json.JsonSerializerOptions JsonOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        FoundationExtensions.ConfigureJson(options);
        return options;
    }
}
