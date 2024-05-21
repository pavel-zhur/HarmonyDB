namespace OneShelf.Common.Api.Client;

public class ApiClientOptions<TClient>
{
    public required Uri Endpoint { get; set; }

    public required string MasterCode { get; set; }
}