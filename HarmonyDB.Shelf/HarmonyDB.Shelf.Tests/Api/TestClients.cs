namespace HarmonyDB.Shelf.Tests.Api;

/// <summary>
/// The settings the app needs to start that every test host gives: the services the shelf calls, at an address where
/// nothing answers (a call is refused at once), unless a test puts its own fake behind a client; the media addresses'
/// key; the public page's address.
/// </summary>
public static class TestClients
{
    public const string Key = "an-internal-key-of-the-tests-0123456789";

    private const string Nowhere = "http://127.0.0.1:1";

    public static IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["Clients:Audio:BaseUrl"] = Nowhere,
        ["Clients:Audio:Key"] = Key,
        ["Clients:Index:Endpoint"] = $"{Nowhere}/index/api/",
        ["Clients:Index:Key"] = Key,
        ["Clients:OwnChords:Endpoint"] = $"{Nowhere}/collectives/api/",
        ["Clients:OwnChords:Key"] = Key,
        ["Clients:OwnChords:PreviewEndpoint"] = $"{Nowhere}/sourcesself/api/",
        ["Clients:OwnChords:PreviewKey"] = Key,
        ["Clients:Illustrations:Endpoint"] = $"{Nowhere}/illustrations/api/",
        ["Clients:Illustrations:Key"] = Key,
        ["Clients:Pdfs:Endpoint"] = $"{Nowhere}/pdfs/api/",
        ["Clients:Pdfs:Key"] = Key,
        ["PublicPage:Origin"] = "https://site.example",
        ["Media:SigningKey"] = Convert.ToBase64String(Enumerable.Range(100, 32).Select(x => (byte)x).ToArray()),
    };
}
