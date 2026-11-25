namespace HarmonyDB.Common.ServiceAuth;

/// <summary>
/// A service allowed to call this one: its name, its key, and the scopes (areas of this service) it may use.
/// Configured as <c>InternalClients:[{Name, Key, Scopes[]}]</c>; keys live only in configuration.
/// </summary>
public sealed class InternalClient
{
    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public List<string> Scopes { get; set; } = [];
}
