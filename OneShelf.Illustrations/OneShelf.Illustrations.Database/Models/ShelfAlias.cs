namespace OneShelf.Illustrations.Database.Models;

/// <summary>
/// The other name of an item while both shelves run: the new shelf knows chords by their id (<see cref="Key"/>), the old
/// one by their address (<see cref="Url"/>), and the illustrations made in the old shelf are kept under the address. The
/// new shelf's operations follow the alias, so an item shows its old illustrations and adds new ones to the same set.
/// This goes when the old shelf is retired: the rows are then re-keyed to the ids and the aliases dropped.
/// </summary>
public class ShelfAlias
{
    public required string Key { get; init; }

    public required string Url { get; set; }
}
