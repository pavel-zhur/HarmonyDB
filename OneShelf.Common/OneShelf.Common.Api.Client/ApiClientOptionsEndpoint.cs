namespace OneShelf.Common.Api.Client;

public class ApiClientOptionsEndpoint<TClient>
{
    public Uri? Endpoint { get; set; }

    public string? MasterCode { get; set; }

    public string? ServiceCode { get; set; }
}