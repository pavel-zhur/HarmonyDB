using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Music;

/// <summary>
/// The language the music services' names are asked in: the app's, else the person's, else the browser's.
/// </summary>
public sealed class MusicLanguagesTests(ShelfDatabaseFixture database) : IClassFixture<ShelfDatabaseFixture>
{
    [DatabaseFact]
    public async Task TheAppsLanguageElseThePersonsElseTheBrowsers()
    {
        var russian = new Account { Id = Guid.NewGuid(), DisplayName = "ru", Locale = "ru", CreatedAt = DateTimeOffset.UtcNow };
        var unset = new Account { Id = Guid.NewGuid(), DisplayName = "unset", CreatedAt = DateTimeOffset.UtcNow };
        await using (var db = database.CreateContext())
        {
            db.Accounts.AddRange(russian, unset);
            await db.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var languages = new MusicLanguages(context);
        ClaimsPrincipal Person(Account x) => new(ShelfIdentity.Create("tests", x.Id, [], null));
        var guest = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Equal("en", await languages.OfAsync(Person(russian), "en", "ru", CancellationToken.None));
        Assert.Equal("ru", await languages.OfAsync(Person(russian), null, "en", CancellationToken.None));
        Assert.Equal("ru", await languages.OfAsync(Person(unset), null, "de-DE, ru-RU;q=0.8, en;q=0.5", CancellationToken.None));
        Assert.Equal("ru", await languages.OfAsync(guest, null, "en;q=0.3, ru", CancellationToken.None));
        Assert.Equal("en", await languages.OfAsync(guest, null, "de", CancellationToken.None));
        Assert.Equal("en", await languages.OfAsync(guest, null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ShelfException>(() => languages.OfAsync(guest, "de", null, CancellationToken.None));
    }

    [Fact]
    public async Task TheGenresAreAskedOfTheMusicServiceInThatLanguage()
    {
        var asked = new List<string>();
        var factory = new ShelfApiFactory();
        factory.Settings["Clients:Audio:BaseUrl"] = "http://audio.test";
        factory.Settings["Clients:Audio:Key"] = "an-internal-key-of-the-tests-0123456789";
        using var api = factory.WithWebHostBuilder(x => x.ConfigureTestServices(services =>
            services.AddHttpClient<AudioMusicClient>().ConfigurePrimaryHttpMessageHandler(() => new Recording(asked))));
        using var client = api.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/music/yandex/categories");
        request.Headers.AcceptLanguage.ParseAdd("ru-RU");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/music/youtube/categories/abc?locale=en")).StatusCode);
        await Browser.AssertError(await client.GetAsync("/api/music/yandex/categories?locale=de"), HttpStatusCode.BadRequest, Model.ErrorCodes.BadRequest);

        Assert.Equal(["/internal/yandex/categories?lang=ru", "/internal/youtube/categories/abc?lang=en"], asked);
    }

    private sealed class Recording(List<string> asked) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            asked.Add(request.RequestUri!.PathAndQuery);
            object data = request.RequestUri.AbsolutePath.StartsWith("/internal/yandex", StringComparison.Ordinal) ? new { genres = Array.Empty<object>() } : new { sections = Array.Empty<object>() };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { storedAt = DateTimeOffset.UtcNow, fetchedNow = true, data }) });
        }
    }
}
