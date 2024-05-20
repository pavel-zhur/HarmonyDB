namespace HarmonyDB.Sources.Api.Model.V1.Api;

public class GetSourcesAndExternalIdsResponse
{
    public required Dictionary<string, UriAttributes> Attributes { get; init; }
}