using OneShelf.Common.Api.Client;

namespace OneShelf.Illustrations.Api.Client;

public class IllustrationsApiClientOptions : ApiClientOptions<IllustrationsApiClient>
{
    public required string? GetImagePublicCode { get; set; }
}