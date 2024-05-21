using Microsoft.Extensions.Options;
using OneShelf.Common.Api.Client;
using OneShelf.Illustrations.Api.Model;

namespace OneShelf.Illustrations.Api.Client;

public class IllustrationsApiClient : ApiClientBase<IllustrationsApiClient>
{
    private readonly IllustrationsApiClientOptions _options;

    public IllustrationsApiClient(IHttpClientFactory httpClientFactory, IOptions<IllustrationsApiClientOptions> options)
        : base(options, httpClientFactory)
    {
        _options = options.Value;
    }

    public async Task<AllResponse> Generate(string url, int version, long? userId, string? additionalBillingInfo, List<GenerationIndex>? generateIndices = null, string? alterationKey = null)
        => await PostWithCode<GenerateRequest, AllResponse>(IllustrationsApiUrls.Generate, new()
        {
            GenerateIndices = generateIndices,
            Url = url,
            SpecialSystemMessage = version,
            UserId = userId,
            AdditionalBillingInfo = additionalBillingInfo,
            AlterationKey = alterationKey,
        });

    public async Task<AllResponse> Generate(string url, string customSystemMessage, long? userId, string? additionalBillingInfo, List<GenerationIndex>? generateIndices = null)
        => await PostWithCode<GenerateRequest, AllResponse>(IllustrationsApiUrls.Generate, new()
        {
            GenerateIndices = generateIndices,
            Url = url,
            CustomSystemMessage = customSystemMessage,
            UserId = userId,
            AdditionalBillingInfo = additionalBillingInfo,
        });

    public async Task<AllResponse> All()
        => await PostWithCode<AllRequest, AllResponse>(IllustrationsApiUrls.All, new());

    public async Task<GetNonEmptyUrlsResponse> GetNonEmptyUrls()
        => await PostWithCode<GetNonEmptyUrlsRequest, GetNonEmptyUrlsResponse>(IllustrationsApiUrls.GetNonEmptyUrls, new());

    public async Task<byte[]> GetImage(Guid id)
        => await PostWithCode<GetImageRequest>(IllustrationsApiUrls.GetImage, new()
        {
            Id = id,
        });

    public string? GetGetImagePublicUrl(Guid id)
    {
        return _options.GetImagePublicCode == null ? null : $"{new Uri(_options.Endpoint, IllustrationsApiUrls.GetImagePublic)}?code={_options.GetImagePublicCode}&id={id}";
    }
}