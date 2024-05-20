namespace OneShelf.Common.Cosmos.Options;

public class CosmosDatabaseOptions
{
    public required string EndPointUri { get; init; }

    public required string PrimaryKey { get; init; }

    public required string DatabaseName { get; init; }
}