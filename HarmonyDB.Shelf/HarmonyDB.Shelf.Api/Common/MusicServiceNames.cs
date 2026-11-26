using Stored = HarmonyDB.Shelf.Database.Model.MusicService;
using Shown = HarmonyDB.Shelf.Model.Me.MusicService;

namespace HarmonyDB.Shelf.Api.Common;

/// <summary>
/// The music services the shelf plays, by their names: the names the sources give them in the contract, the API's
/// addresses use and the database stores. The one place a name is turned into the shelf's service and back.
/// </summary>
public static class MusicServiceNames
{
    private static readonly IReadOnlyList<(string Name, Shown Service, Stored Stored)> All =
    [
        ("yandex", Shown.Yandex, Stored.Yandex),
        ("youtube", Shown.Youtube, Stored.Youtube),
    ];

    /// <returns>Null for a name of another service.</returns>
    public static Shown? Of(string name) => All.Where(x => x.Name == name).Select(x => (Shown?)x.Service).SingleOrDefault();

    /// <returns>Null for a name of another service.</returns>
    public static Stored? StoredOf(string name) => All.Where(x => x.Name == name).Select(x => (Stored?)x.Stored).SingleOrDefault();

    public static string NameOf(Shown service) => All.Single(x => x.Service == service).Name;

    public static string NameOf(Stored service) => All.Single(x => x.Stored == service).Name;
}
