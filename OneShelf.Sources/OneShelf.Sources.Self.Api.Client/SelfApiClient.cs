using HarmonyDB.Common.Representations.OneShelf;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api.Client;
using OneShelf.Sources.Self.Api.Model;
using OneShelf.Sources.Self.Api.Model.V1;

namespace OneShelf.Sources.Self.Api.Client;

public class SelfApiClient : ApiClientBase<SelfApiClient>
{
    public SelfApiClient(IHttpClientFactory httpClientFactory, IOptions<ApiClientOptions<SelfApiClient>> options)
        : base(options, httpClientFactory)
    {
    }

    public async Task<NodeHtml> V1FormatPreview(FormatPreviewRequest request)
        => (await PostWithCode<FormatPreviewRequest, FormatPreviewResponse>(SelfApiUrls.V1FormatPreview, request)).Output;
}