using OneShelf.Common.Cosmos.Options;

namespace OneShelf.Collectives.Database.Options;

public class CollectivesCosmosDatabaseOptions : CosmosDatabaseOptions
{
    public string? ContainerNamePostfix { get; set; }
}