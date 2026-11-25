namespace HarmonyDB.Shelf.Model.Book;

/// <summary>
/// A book of chords: these items, in this order, with these options. The same for every place a book is made from — a
/// playlist, a search, a search inside a playlist, one item: the frontend sends the items chosen, in the order chosen.
/// </summary>
public record BookRequest
{
    /// <summary>
    /// The chords, in the book's order.
    /// </summary>
    public required IReadOnlyList<BookItem> Items { get; init; }

    /// <summary>
    /// The playlist the items come from, if they do: each item's key is then the person's key for it in this playlist
    /// ("here"), when they set one.
    /// </summary>
    public Guid? PlaylistId { get; init; }

    public required BookOptions Options { get; init; }
}

public record BookItem
{
    /// <summary>
    /// The chords' id.
    /// </summary>
    public required string ChordsId { get; init; }

    /// <summary>
    /// The key to print in, in semitones (−11 … 11); null: the person's key for these chords — their "here" key in the
    /// playlist, else their own ("mine"), else as written.
    /// </summary>
    public int? Transpose { get; init; }

    /// <summary>
    /// Printed in two columns: fewer pages for a long song with short lines.
    /// </summary>
    public bool TwoColumns { get; init; }

    /// <summary>
    /// Its number in the list it comes from, printed when the book is not numbered anew (see
    /// <see cref="BookOptions.Renumber"/>); null: its place in this book.
    /// </summary>
    public int? Number { get; init; }
}

/// <summary>
/// How a key's sharps and flats are written.
/// </summary>
public enum BookAccidentals
{
    /// <summary>As each chord is written.</summary>
    AsWritten,
    Flats,
    Sharps,
}

public record BookOptions
{
    /// <summary>
    /// The title on the cover. With it, the book has a cover, each song's number on its pages, and an empty page where
    /// one is needed for a song of two pages to open as a spread. Without it, the songs are one after another.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// A page of contents after the cover (with a caption only): the songs by their artists, with their numbers.
    /// </summary>
    public bool Contents { get; init; }

    /// <summary>
    /// The songs numbered 1, 2, 3 … in the book's order; otherwise each keeps its number in its list (with gaps where
    /// songs were left out).
    /// </summary>
    public bool Renumber { get; init; }

    /// <summary>
    /// Sharps or flats in the chords; null: as the person's settings say.
    /// </summary>
    public BookAccidentals? Accidentals { get; init; }
}

/// <summary>
/// What became of one item in a book (or in counting its pages).
/// </summary>
public enum BookItemState
{
    /// <summary>In the book.</summary>
    Printed,

    /// <summary>Not open to the person (or not known): left out.</summary>
    NotOpen,

    /// <summary>Its source did not answer: left out; try again later.</summary>
    Unavailable,
}

public record BookItemPages
{
    public required string ChordsId { get; init; }

    public required BookItemState State { get; init; }

    /// <summary>
    /// The key it is printed in, in semitones.
    /// </summary>
    public required int Transpose { get; init; }

    /// <summary>
    /// Its pages in one column and in two; given when it is printed.
    /// </summary>
    public int? PagesOneColumn { get; init; }

    public int? PagesTwoColumns { get; init; }
}

/// <summary>
/// What a job of the kind "book_pages" ends with: how many pages each item takes, to lay the book out.
/// </summary>
public record BookPagesResult
{
    /// <summary>
    /// In the order asked.
    /// </summary>
    public required IReadOnlyList<BookItemPages> Items { get; init; }
}

public record BookItemResult
{
    public required string ChordsId { get; init; }

    public required BookItemState State { get; init; }

    /// <summary>
    /// Its number in the book, when it is printed.
    /// </summary>
    public int? Number { get; init; }

    public required int Transpose { get; init; }
}

/// <summary>
/// What a job of the kind "book" ends with; the PDF is the job's file.
/// </summary>
public record BookResult
{
    public required int Pages { get; init; }

    /// <summary>
    /// In the order asked.
    /// </summary>
    public required IReadOnlyList<BookItemResult> Items { get; init; }
}

public static class BookErrorCodes
{
    /// <summary>400: no items, or more than one book takes (details: <c>max</c>).</summary>
    public const string BadItems = "book_bad_items";

    /// <summary>400: a key out of −11 … 11, or a number below 1.</summary>
    public const string BadItem = "book_bad_item";

    /// <summary>A job's failure: none of the items could be printed.</summary>
    public const string NothingToPrint = "book_nothing_to_print";
}
