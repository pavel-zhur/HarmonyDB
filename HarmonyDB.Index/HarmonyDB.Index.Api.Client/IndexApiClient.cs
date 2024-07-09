using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VInternal;
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

    public async Task<SongsByChordsResponse> SongsByChords(SongsByChordsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<SongsByChordsRequest, SongsByChordsResponse>(IndexApiUrls.VExternal1SongsByChords, request, apiTraceBag: apiTraceBag);

    public async Task<SongsByHeaderResponse> SongsByHeader(SongsByHeaderRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<SongsByHeaderRequest, SongsByHeaderResponse>(IndexApiUrls.VExternal1SongsByHeader, request, apiTraceBag: apiTraceBag);

    public async Task<LoopsResponse> Loops(LoopsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<LoopsRequest, LoopsResponse>(IndexApiUrls.VExternal1Loops, request, apiTraceBag: apiTraceBag);

    public async Task<StructureLoopsResponse> StructureLoops(StructureLoopsRequest request, ApiTraceBag? apiTraceBag = null)
        => await PostWithCode<StructureLoopsRequest, StructureLoopsResponse>(IndexApiUrls.VExternal1StructureLoops, request, apiTraceBag: apiTraceBag);
}