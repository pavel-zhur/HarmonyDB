namespace OneShelf.Authorization.Api.Client;

public class AuthorizationApiClientOptions
{
    public Uri CheckIdentityEndpoint { get; set; }

    public Uri PingEndpoint { get; set; }

    public string ServiceCode { get; set; }
}