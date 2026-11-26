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

        // a changed address opens nothing
        var bytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(token);
        bytes[^20] ^= 1;
        Assert.Equal(404, Assert.Throws<ShelfException>(() => signer.Verify(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes))).StatusCode);

        // signed under another key
        var other = new MediaSigner(new Monitor(new() { SigningKey = Convert.ToBase64String(new byte[32]) }), time);
        Assert.Equal(404, Assert.Throws<ShelfException>(() => other.Verify(token)).StatusCode);

        time.Now += TimeSpan.FromMinutes(16);
        var expired = Assert.Throws<ShelfException>(() => signer.Verify(token));
        Assert.Equal((410, MediaErrorCodes.Expired), (expired.StatusCode, expired.Code));
    }

    [Fact]
    public void NobodyReadsWhomAFileIsPlayedFor()
    {
        var signer = new MediaSigner(new Monitor(new() { SigningKey = SigningKey }), TimeProvider.System);
        var address = new Uri("https://site.example/chords/ha-1?code=314159");
        var caller = new Caller { Subjects = ["account:0d6a9f8e-4f43-4a51-9f2b-1c1e3b6a7d55"], Keys = ["a-secret-key"] };
        var token = signer.Sign(new() { Kind = MediaTarget.File, Id = "ha-1", Name = "recording.flac", Caller = caller, Address = address }, signer.NewExpiry())[MediaSigner.PathPrefix.Length..];

        var text = System.Text.Encoding.UTF8.GetString(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(token));
        Assert.DoesNotContain("a-secret-key", text);
        Assert.DoesNotContain("0d6a9f8e", text);
        Assert.DoesNotContain("recording", text);
        Assert.DoesNotContain("314159", text);

        var (target, _) = signer.Verify(token);
        Assert.Equal(("file", "ha-1", "recording.flac", caller.Subjects.Single(), "a-secret-key", address),
            (target.Kind, target.Id, target.Name, target.Caller!.Subjects.Single(), target.Caller.Keys.Single(), target.Address));
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
    public async Task AnItemsFilesAreGivenOnlyWhenItIsOpenAndPlayedFromItsSourceForTheAsker()
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
                            Audio = new() { File = new() { Name = "recording.mp3", ContentType = "audio/mpeg" } },
                            Duration = 200,
                            Stems =
                            [
                                new() { Stem = Stem.Bass, Audio = new() { File = new() { Name = "bass.mp3", ContentType = "audio/mpeg" } } },
                                // a service the shelf does not play is passed over
                                new() { Stem = Stem.Drums, Audio = new() { Track = new() { Service = "elsewhere", Id = "1" } } },
                            ],
                        },
                    },
                })
                : (HttpStatusCode.OK, new GetContentResponse { Id = "closed", State = ItemState.NotOpen }),
            FileAnswer = (request, _) =>
            {
                var answer = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent([1, 2, 3, 4]) };
                answer.Content.Headers.ContentType = new("audio/mpeg");
                answer.Content.Headers.ContentRange = new(0, 3, 10);
                answer.Headers.ETag = new("\"abc\"");
                Assert.Equal("bytes=0-3", request.Headers.GetValues("Range").Single());
                return answer;
            },
        };
        using var api = index.Api(new ShelfApiFactory(), new Dictionary<string, string?> { ["Media:SigningKey"] = SigningKey });
        using var client = api.CreateClient();
        var person = Guid.NewGuid();

        var open = await client.SendAsync(FakeIndex.As(HttpMethod.Post, "/api/media/chords", person, body: new ChordsMediaRequest { Id = "open" }));
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        var media = (await open.Content.ReadFromJsonAsync<ChordsMedia>(Browser.Json))!;
        Assert.NotNull(media.RecordingUrl);
        Assert.Equal(MediaStem.Bass, media.Stems.Single().Stem);
        Assert.Equal(200, media.DurationSeconds);

        // played as a guest (an address is played by the browser's audio element): the source is asked for the person
        // the address was made for
        var play = new HttpRequestMessage(HttpMethod.Get, media.RecordingUrl);
        play.Headers.Range = new(0, 3);
        var played = await client.SendAsync(play);
        Assert.Equal(HttpStatusCode.PartialContent, played.StatusCode);
        Assert.Equal([1, 2, 3, 4], await played.Content.ReadAsByteArrayAsync());
        Assert.Equal("bytes 0-3/10", played.Content.Headers.ContentRange!.ToString());
        Assert.Equal("\"abc\"", played.Headers.ETag!.Tag);
        var asked = index.Requests.Single(x => x.Operation == SourceV2Urls.V2GetFile).Body;
        Assert.Equal(("open", "recording.mp3", $"account:{person}"),
            (asked.GetProperty("id").GetString(), asked.GetProperty("name").GetString(), asked.GetProperty("caller").GetProperty("subjects")[0].GetString()));

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
