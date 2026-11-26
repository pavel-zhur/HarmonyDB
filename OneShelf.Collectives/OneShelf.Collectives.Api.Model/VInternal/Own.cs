using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.VInternal;

/// <summary>
/// Who acts on own chords: opaque names of one person, as the calling service gives them, each once. The first is the
/// person's name now: what they write is authored by it. Own chords are theirs when any of the names is their author.
/// Chords written in the old shelf are authored by <c>telegram:</c> and the Telegram user's id.
/// </summary>
public class OwnChordsAuthor
{
    public required List<string> Subjects { get; set; }
}
/// <summary>
/// Own chords: the latest version, and who wrote them.
/// </summary>
public class OwnCollective
{
    public required Guid CollectiveId { get; set; }

    public required Collective Collective { get; set; }

    public required int VersionNumber { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required DateTime UpdatedOn { get; set; }

    public required int SearchTag { get; set; }

    public required Uri Uri { get; set; }

    /// <summary>
    /// Who wrote them: an opaque name (see <see cref="OwnChordsAuthor"/>).
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// Whether they can be changed and deleted here. Those written in the old shelf cannot: they are changed there.
    /// </summary>
    public required bool Editable { get; set; }

    /// <summary>
    /// Every version, the first first; given by <see cref="CollectivesApiUrls.OwnGet"/> only.
    /// </summary>
    public List<OwnCollectiveVersion>? History { get; set; }
}

public class OwnCollectiveVersion
{
    public required int VersionNumber { get; set; }

    public required DateTime CreatedOn { get; set; }

    public required Collective Collective { get; set; }
}

public class OwnListRequest
{
    public required OwnChordsAuthor Author { get; set; }
}

public class OwnListResponse
{
    /// <summary>
    /// The author's own chords that are not deleted.
    /// </summary>
    public required List<OwnCollective> Items { get; set; }
}

public class OwnGetRequest
{
    public required OwnChordsAuthor Author { get; set; }

    public required Guid CollectiveId { get; set; }
}

public class OwnInsertRequest
{
    /// <summary>
    /// The writer: the first of the names is the author.
    /// </summary>
    public required OwnChordsAuthor Author { get; set; }

    public required Collective Collective { get; set; }
}

public class OwnUpdateRequest
{
    public required OwnChordsAuthor Author { get; set; }

    public required Guid CollectiveId { get; set; }

    public required Collective Collective { get; set; }
}

public class OwnDeleteRequest
{
    public required OwnChordsAuthor Author { get; set; }

    public required Guid CollectiveId { get; set; }
}

/// <summary>
/// Removes for good, every version with its text, the private own chords written here by any of the names: those
/// private now and those deleted while private. Others (club, public, written in the old shelf) stay. Done again, it
/// removes what is left.
/// </summary>
public class OwnRemovePrivateRequest
{
    public required OwnChordsAuthor Author { get; set; }
}

public class OwnRemovePrivateResponse
{
    /// <summary>
    /// How many were removed now.
    /// </summary>
    public required int Removed { get; set; }
}

public class ItemsRequest
{
    /// <summary>
    /// The ids to read; null for every own chords there are.
    /// </summary>
    public List<Guid>? CollectiveIds { get; set; }
}

public class ItemsResponse
{
    /// <summary>
    /// The own chords asked that exist and are not deleted, of both kinds of authors; the others are left out.
    /// </summary>
    public required List<OwnCollective> Items { get; set; }
}
