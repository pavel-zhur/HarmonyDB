using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.VInternal;

/// <summary>
/// Who acts on own chords: an account of the new shelf, and the Telegram users it is (its Telegram sign-ins), as the
/// calling service tells. The chords written by that account, and those written by those Telegram users in the old
/// shelf, are theirs.
/// </summary>
public class OwnChordsAuthor
{
    public required Guid AccountId { get; set; }

    public required List<long> TelegramUserIds { get; set; }
}

/// <summary>
/// Own chords: the latest version, and who wrote them (a Telegram user, for those written in the old shelf, or an
/// account of the new shelf).
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

    public long? AuthorTelegramUserId { get; set; }

    public Guid? AuthorAccountId { get; set; }

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
    public required Guid AccountId { get; set; }

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
/// The own chords one account wrote become another's (the first is merged into the second).
/// </summary>
public class OwnMoveRequest
{
    public required Guid FromAccountId { get; set; }

    public required Guid ToAccountId { get; set; }
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
