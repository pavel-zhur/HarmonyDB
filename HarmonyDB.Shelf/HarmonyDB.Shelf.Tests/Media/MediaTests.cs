using System.Net;
using System.Net.Http.Json;
using HarmonyDB.Shelf.Api.Areas.Media.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model.Media;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Shelf.Tests.Auth;
using HarmonyDB.Shelf.Tests.Chords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using HarmonyDB.Source.Api.Model.V2.Contents;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Tests.Media;

/// <summary>
/// Media addresses: signed, expiring, given to guests, limited per client address, and given for an item's recording
/// only when the item is open to the asker.
/// </summary>
public sealed class MediaTests
{
    private static readonly string SigningKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(x => (byte)x).ToArray());

    [Fact]
    public void AnAddressWorksUntilItExpiresAndOnlyAsSigned()
    {
        var time = new ManualTime(DateTimeOffset.Parse("2026-11-25T10:00:00Z"));
        var signer = new MediaSigner(new Monitor(new() { SigningKey = SigningKey }), time);
        var url = signer.Sign(new() { Kind = MediaTarget.Audio, Service = "yandex", Id = "42" }, signer.NewExpiry());
        var token = url[MediaSigner.PathPrefix.Length..];

        var (target, _) = signer.Verify(token);
        Assert.Equal(("audio", "yandex", "42"), (target.Kind, target.Service, target.Id));

        // another target under the same signature
        var dot = token.IndexOf('.');
        var forged = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(token[..dot])).Replace("\"42\"", "\"43\"")));
        Assert.Equal(404, Assert.Throws<ShelfException>(() => signer.Verify(forged + token[dot..])).StatusCode);

        // signed under another key
        var other = new MediaSigner(new Monitor(new() { SigningKey = Convert.ToBase64String(new byte[32]) }), time);
        Assert.Equal(404, Assert.Throws<ShelfException>(() => other.Verify(token)).StatusCode);

        time.Now += TimeSpan.FromMinutes(16);
        var expired = Assert.Throws<ShelfException>(() => signer.Verify(token));
        Assert.Equal((410, MediaErrorCodes.Expired), (expired.StatusCode, expired.Code));
    }

    [Fact]
    public async Task GuestsGetAddressesAndTheirNumberIsLimited()
    {
        using var api = new FakeIndex().Api(new ShelfApiFactory(), new Dictionary<string, string?>
        {
            ["Media:SigningKey"] = SigningKey,
            ["Media:RateLimitPermits"] = "2",
        });
        using var client = api.CreateClient();
        var request = new TrackMediaRequest { Service = Model.Me.MusicService.Youtube, Id = "abc", Kind = MediaKind.Video };

        var first = await client.PostAsJsonAsync("/api/media/tracks", request, Browser.Json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var address = (await first.Content.ReadFromJsonAsync<MediaAddress>(Browser.Json))!;
        Assert.StartsWith("/api/media/", address.Url);
        Assert.True(address.ExpiresAt > DateTimeOffset.UtcNow);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/media/tracks", request, Browser.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/media/tracks", request, Browser.Json)).StatusCode);

        await Browser.AssertError(await client.GetAsync(address.Url[..^2] + "AA"), HttpStatusCode.NotFound, MediaErrorCodes.Invalid);
    }

    [Fact]
    public async Task AnItemsRecordingIsGivenOnlyWhenItIsOpen()
    {
        var index = new FakeIndex
        {
            Answer = (_, body) => body.GetProperty("id").GetString() == "open"
                ? (HttpStatusCode.OK, new GetContentResponse
                {
                    Id = "open",
                    State = ItemState.Open,
                    Header = FakeIndex.Header("open", "Label"),
                    Content = new()
                    {
                        Recording = new()
                        {
                            Audio = new() { Blob = new() { Key = "analyses/a/recording.mp3", ContentType = "audio/mpeg" } },
                            Duration = 200,
                            Stems = [new() { Stem = Stem.Bass, Audio = new() { Blob = new() { Key = "analyses/a/bass.mp3", ContentType = "audio/mpeg" } } }],
                        },
                    },
                })
                : (HttpStatusCode.OK, new GetContentResponse { Id = "closed", State = ItemState.NotOpen }),
        };
        using var api = index.Api(new ShelfApiFactory(), new Dictionary<string, string?> { ["Media:SigningKey"] = SigningKey });
        using var client = api.CreateClient();

        var open = await client.PostAsJsonAsync("/api/media/chords", new ChordsMediaRequest { Id = "open" }, Browser.Json);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var media = (await open.Content.ReadFromJsonAsync<ChordsMedia>(Browser.Json))!;
        Assert.NotNull(media.RecordingUrl);
        Assert.Equal(MediaStem.Bass, media.Stems.Single().Stem);
        Assert.Equal(200, media.DurationSeconds);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/media/chords", new ChordsMediaRequest { Id = "closed" }, Browser.Json)).StatusCode);
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Monitor(MediaOptions value) : IOptionsMonitor<MediaOptions>
    {
        public MediaOptions CurrentValue => value;

        public MediaOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<MediaOptions, string?> listener) => null;
    }
}
