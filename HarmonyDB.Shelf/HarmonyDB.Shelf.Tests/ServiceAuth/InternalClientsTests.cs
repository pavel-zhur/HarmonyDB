using System.Net;
using HarmonyDB.Common.ServiceAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Tests.ServiceAuth;

public sealed class InternalClientsTests : IAsyncLifetime
{
    private const string AdminKey = "admin-key-0123456789abcdefghijklmnopqrstuvwxyz";
    private const string SourceKey = "source-key-0123456789abcdefghijklmnopqrstuvwxyz";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["InternalClients:0:Name"] = "admin",
            ["InternalClients:0:Key"] = AdminKey,
            ["InternalClients:0:Scopes:0"] = "shelf.admin",
            ["InternalClients:1:Name"] = "source",
            ["InternalClients:1:Key"] = SourceKey,
            ["InternalClients:1:Scopes:0"] = "shelf.source",
        });
        builder.Services.AddInternalClients(builder.Configuration);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGet("/admin", (HttpContext context) => context.User.Identity!.Name).RequireInternalScope("shelf.admin");
        _app.MapGet("/attribute", [RequireInternalScope("shelf.source")] (HttpContext context) => context.User.Identity!.Name);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task NoKeyIsUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task UnknownKeyIsUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/admin", AdminKey + "x")).StatusCode);
    }

    [Fact]
    public async Task KeyWithTheScopeIsTheClient()
    {
        var response = await Get("/admin", AdminKey);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("admin", await response.Content.ReadAsStringAsync());

        var byAttribute = await Get("/attribute", SourceKey);
        Assert.Equal("source", await byAttribute.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KeyWithoutTheScopeIsForbidden()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/admin", SourceKey)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/attribute", AdminKey)).StatusCode);
    }

    [Fact]
    public void ShortKeyIsRefused() => AssertRefused(("a", "too-short"), ("b", SourceKey));

    [Fact]
    public void SameNameIsRefused() => AssertRefused(("a", AdminKey), ("a", SourceKey));

    [Fact]
    public void SameKeyIsRefused() => AssertRefused(("a", AdminKey), ("b", AdminKey));

    private static void AssertRefused(params (string Name, string Key)[] clients)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < clients.Length; i++)
        {
            settings[$"InternalClients:{i}:Name"] = clients[i].Name;
            settings[$"InternalClients:{i}:Key"] = clients[i].Key;
        }

        var services = new ServiceCollection();
        services.AddInternalClients(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<InternalClientsOptions>>().Value);
    }

    private Task<HttpResponseMessage> Get(string path, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(InternalKeyDefaults.HeaderName, key);
        return _client.SendAsync(request);
    }
}
