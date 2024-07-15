using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Main;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;
using HarmonyDB.Index.Api.Model.VInternal;
using HarmonyDB.Source.Api.Model;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api.Client;

namespace HarmonyDB.Index.Api.Client;

public class IndexApiClient : ApiClientBase<IndexApiClient>
{
    public IndexApiClient(IHttpClientFactory httpClientFactory, IOptions<ApiClientOptions<IndexApiClient>> options)
        : base(options, httpClientFactory)
    {
    }

    public async Task<string?> GetLyrics(string url)
        => (await PostWithCode<GetLyricsRequest, GetLyricsResponse>(IndexApiUrls.VInternalGetLyrics, new()
        {
            Url = url,
        })).Lyrics;

    public async Task<TryImportResponse> TryImport(string url)
        => await PostWithCode<TryImportRequest, TryImportResponse>(IndexApiUrls.VInternalTryImport, new()
        {
            Url = url,
        });

    public async Task PingAll()
    {
        if (Options.ConditionalStreams?.Any() != true
            && Options.Endpoint == null)
        {
            return;
        }

        await Task.WhenAll((Options.ConditionalStreams?.Keys ?? Enumerable.Empty<string>())
            .Select(id => Ping(SourceApiUrls.V1Ping, id)));
    }

    public async Task<SongsByChordsResponse> SongsByChords(SongsByChordsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<SongsByChordsRequest, SongsByChordsResponse>(IndexApiUrls.VExternal1SongsByChords, request, apiTraceBag: apiTraceBag, conditionalStreamId: "B");

    public async Task<SongsByHeaderResponse> SongsByHeader(SongsByHeaderRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<SongsByHeaderRequest, SongsByHeaderResponse>(IndexApiUrls.VExternal1SongsByHeader, request, apiTraceBag: apiTraceBag, conditionalStreamId: "B");

    public async Task<Model.VExternal1.Main.LoopsResponse> Loops(Model.VExternal1.Main.LoopsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<Model.VExternal1.Main.LoopsRequest, Model.VExternal1.Main.LoopsResponse>(IndexApiUrls.VExternal1Loops, request, apiTraceBag: apiTraceBag, conditionalStreamId: "B");

    public async Task<Model.VExternal1.Tonalities.LoopsResponse> TonalitiesLoops(Model.VExternal1.Tonalities.LoopsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<Model.VExternal1.Tonalities.LoopsRequest, Model.VExternal1.Tonalities.LoopsResponse>(IndexApiUrls.VExternal1TonalitiesLoops, request, apiTraceBag: apiTraceBag, conditionalStreamId: "A");

    public async Task<SongsResponse> TonalitiesSongs(SongsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<SongsRequest, SongsResponse>(IndexApiUrls.VExternal1TonalitiesSongs, request, apiTraceBag: apiTraceBag, conditionalStreamId: "A");

    public async Task<LoopResponse> TonalitiesLoop(LoopRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<LoopRequest, LoopResponse>(IndexApiUrls.VExternal1TonalitiesLoop, request, apiTraceBag: apiTraceBag, conditionalStreamId: "A");

    public async Task<SongResponse> TonalitiesSong(SongRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<SongRequest, SongResponse>(IndexApiUrls.VExternal1TonalitiesSong, request, apiTraceBag: apiTraceBag, conditionalStreamId: "A");
}