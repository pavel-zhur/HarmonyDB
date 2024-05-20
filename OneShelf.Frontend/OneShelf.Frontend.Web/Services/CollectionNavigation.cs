using Microsoft.AspNetCore.Components;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;
using OneShelf.Frontend.Web.Shared;
using Collection = OneShelf.Frontend.Web.Pages.Collection;
using SongsMode = OneShelf.Frontend.Web.Shared.SearchResultsLocal.SongsMode;
using Version = OneShelf.Frontend.Api.Model.V3.Databasish.Version;

namespace OneShelf.Frontend.Web.Services;

public class CollectionNavigation
{
    private readonly ILogger<CollectionNavigation> _logger;
    private readonly NavigationManager _navigationManager;
    private readonly IdentityProvider _identityProvider;

    public CollectionNavigation(ILogger<CollectionNavigation> logger, NavigationManager navigationManager, IdentityProvider identityProvider)
    {
        _logger = logger;
        _navigationManager = navigationManager;
        _identityProvider = identityProvider;
    }

    public string CollectiveAdd(int versionId)
        => $"/collective?action=Add&versionId={versionId}";

    public string CollectiveDelete(Guid collectiveId)
        => $"/collective?action=Delete&collectiveId={collectiveId}";

    public string CollectiveUpdate(Guid collectiveId)
        => $"/collective?action=Update&collectiveId={collectiveId}";

    public string CollectiveRefresh(Guid collectiveId)
        => $"/collective?action=Refresh&collectiveId={collectiveId}";

    public string ChordsListLink(string? externalId, int? versionId)
        => _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.ExternalId), externalId },
            { nameof(Collection.VersionId), versionId },
            { nameof(Collection.IsOpen2), true },
        });

    public string IllustrationsLink()
        => _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.ExternalId), null },
            { nameof(Collection.VersionId), null },
            { nameof(Collection.IsOpen2), true },
        });

    public string SearchResultsInternetJustHighlight(string virtualSongArtist, string virtualSongTitle) =>
        _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.VirtualSongArtist), virtualSongArtist },
            { nameof(Collection.VirtualSongTitle), virtualSongTitle },
            { nameof(Collection.SongId), null },
        });

    public string SearchResultsInternetLink(string virtualSongArtist, string virtualSongTitle, string externalId) =>
        _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.VirtualSongArtist), virtualSongArtist },
            { nameof(Collection.VirtualSongTitle), virtualSongTitle },
            { nameof(Collection.IsOpen), true },
            { nameof(Collection.IsOpen2), true },
            { nameof(Collection.ExternalId), externalId },
            { nameof(Collection.VersionId), null },
            { nameof(Collection.SongId), null },
        });

    public string SearchResultsInternetMode(SearchResultsInternet.SongsMode songsMode)
        => _navigationManager.GetUriWithQueryParameter(nameof(Collection.ModeInternet), songsMode.ToString());

    public string SearchResultsLocalLink(ISong song)
    {
        Version? selectVersion = null;
        if (song.Versions.Count == 1)
        {
            selectVersion = song.Versions.Single();
        }
        else if (song.Versions.Any())
        {
            selectVersion = song.Versions
                .OrderBy(x => x.CollectiveId.HasValue ? x.UserId == _identityProvider.RequiredIdentity.Id ? 1 : 2 : 3)
                .ThenByDescending(x => song.Likes.SingleOrDefault(l => l.VersionId == x.Id && l.UserId == _identityProvider.RequiredIdentity.Id && !l.LikeCategoryId.HasValue)?.Level ?? 0)
                .ThenBy(x => x.CreatedOn)
                .First();
        }

        return _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.SongId), song.Id },
            { nameof(Collection.VirtualSongArtist), null },
            { nameof(Collection.VirtualSongTitle), null },
            { nameof(Collection.IsOpen), true },
            { nameof(Collection.IsOpen2), selectVersion != null },
            { nameof(Collection.ExternalId), selectVersion?.ExternalId },
            { nameof(Collection.VersionId), selectVersion?.Id },
        });
    }

    public string SearchResultsLocalIllustrationsLink(ISong song)
    {
        return _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.SongId), song.Id },
            { nameof(Collection.VirtualSongArtist), null },
            { nameof(Collection.VirtualSongTitle), null },
            { nameof(Collection.IsOpen), true },
            { nameof(Collection.IsOpen2), true },
            { nameof(Collection.ExternalId), null },
            { nameof(Collection.VersionId), null },
        });
    }

    public string SearchResultsLocalJustHighlight(int songId)
    {
        return _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.SongId), songId },
            { nameof(Collection.VirtualSongArtist), null },
            { nameof(Collection.VirtualSongTitle), null },
        });
    }

    public string SearchResultsLocalSongChordsRemoved(int songId)
    {
        return _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.SongId), songId },
            { nameof(Collection.VirtualSongArtist), null },
            { nameof(Collection.VirtualSongTitle), null },
            { nameof(Collection.IsOpen), true },
            { nameof(Collection.IsOpen2), false },
            { nameof(Collection.ExternalId), null },
            { nameof(Collection.VersionId), null },
        });
    }

    public string CollectionQueryChange(string? newValue, int? artistId, string? artistName)
    {
        newValue = string.IsNullOrWhiteSpace(newValue) ? null : newValue;
        var noArtist = !artistId.HasValue || artistName == null || !$" {newValue} ".Contains($" {artistName} ");
        return _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.Query), newValue },
            { nameof(Collection.ArtistId), noArtist ? null : artistId },
            { nameof(Collection.IsOpen), false },
            { nameof(Collection.IsOpen2), false },
        });
    }

    public string CollectionClearSearch(string? query, int? artistId, string? artistName)
    {
        var noArtist = !artistId.HasValue || artistName == null || !$" {query} ".Contains($" {artistName} ") || query?.Trim() == artistName;
        return _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.Query), noArtist ? null : artistName },
            { nameof(Collection.ArtistId), noArtist ? null : artistId },
            { nameof(Collection.IsOpen), false },
            { nameof(Collection.IsOpen2), false },
        });
    }

    public string CollectionLayoutStateChanged(bool isOpen, bool isOpen2) =>
        _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.IsOpen), isOpen },
            { nameof(Collection.IsOpen2), isOpen2 },
        });

    public string CollectionSearchInternet(string query) =>
        _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.Query), query },
            { nameof(Collection.IsOpen), false },
            { nameof(Collection.IsOpen2), false },
            { nameof(Collection.ArtistId), null },
            { nameof(Collection.SongId), null },
        });

    public string SearchResultsLocalMode(SongsMode songsMode)
        => _navigationManager.GetUriWithQueryParameter(nameof(Collection.ModeLocal), songsMode.ToString());

    public string SearchResultsLocalShortlistedUser(long shortlistedUserId, bool withReset = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            { nameof(Collection.ShortlistedUserId), shortlistedUserId },
            { nameof(Collection.IsOpen), false },
            { nameof(Collection.IsOpen2), false },
            { nameof(Collection.ModeLocal), SongsMode.NewestLike.ToString() },
        };

        if (withReset)
        {
            parameters[nameof(Collection.Query)] = null;
            parameters[nameof(Collection.LikeCategoryId)] = null;
        }

        return _navigationManager.GetUriWithQueryParameters(parameters);
    }

    public string SearchResultsLocalLikeCategoryId(int? likeCategoryId, bool withReset = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            { nameof(Collection.LikeCategoryId), likeCategoryId },
            { nameof(Collection.IsOpen), false },
            { nameof(Collection.IsOpen2), false },
            { nameof(Collection.ModeLocal), SongsMode.NewestLike.ToString() },
        };

        if (!likeCategoryId.HasValue)
        {
            parameters[nameof(Collection.ModeLocal)] = SongsMode.RatingAll.ToString();
        }

        if (withReset)
        {
            parameters[nameof(Collection.Query)] = null;
            parameters[nameof(Collection.ShortlistedUserId)] = null;
        }

        return _navigationManager.GetUriWithQueryParameters(parameters);
    }

    public string SearchResultsLocalShortlistedUserNone(SongsMode songsMode)
        => _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.ModeLocal), songsMode.ToString() },
            { nameof(Collection.ShortlistedUserId), null },
        });

    public string SearchResultsLocalArtist(int? artistId, string? existingQuery, string artistName)
        => _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.ArtistId), artistId },
            { nameof(Collection.SongId), null },
            { nameof(Collection.VirtualSongArtist), null },
            { nameof(Collection.VirtualSongTitle), null },
            { nameof(Collection.IsOpen), false },
            { nameof(Collection.IsOpen2), false },
            { nameof(Collection.Query), $" {existingQuery} ".Contains($" {artistName} ") ? existingQuery : string.IsNullOrWhiteSpace(existingQuery) ? artistName : $"{artistName} {existingQuery}" }
        });

    public string GetExternalNavigationLink(int artistId, string artistName)
    {
        return $"/collection?{nameof(Collection.ArtistId)}={artistId}&{nameof(Collection.ModeLocal)}={SongsMode.Title}&{nameof(Collection.Query)}={Uri.EscapeDataString(artistName)}";
    }

    public string GetExternalNavigationLink(string query)
    {
        return $"/collection?{nameof(Collection.Query)}={Uri.EscapeDataString(query)}";
    }

    public string GetExternalIllustrationsNavigationLink()
    {
        return $"/collection?{nameof(Collection.ModeLocal)}={SongsMode.WithIllustrations}";
    }

    public string GetExternalMyCollectivesNavigationLink()
    {
        return $"/collection?{nameof(Collection.ModeLocal)}={SongsMode.WithMyCollectives}";
    }

    public string GetExternalNavigationLink(int likeCategoryId)
    {
        return $"/collection?{nameof(Collection.LikeCategoryId)}={likeCategoryId}";
    }

    public string GetExternalNavigationLinkMyRatingNewest()
    {
        return $"/collection?{nameof(Collection.ModeLocal)}={SongsMode.NewestLike}&{nameof(Collection.ShortlistedUserId)}={_identityProvider.RequiredIdentity.Id}";
    }

    public string GetMyContainedString()
    {
        return $"{nameof(Collection.ShortlistedUserId)}={_identityProvider.RequiredIdentity.Id}";
    }

    public string GetExternalNavigationLink(string query, string externalId, string virtualSongArtist, string virtualSongTitle)
    {
        return $"/collection?{nameof(Collection.Query)}={Uri.EscapeDataString(query)}&{nameof(Collection.ExternalId)}={Uri.EscapeDataString(externalId)}&isopen=true&isopen2=true&{nameof(Collection.VirtualSongArtist)}={Uri.EscapeDataString(virtualSongArtist)}&{nameof(Collection.VirtualSongTitle)}={Uri.EscapeDataString(virtualSongTitle)}";
    }

    public string NoShortlistOrLikeCategory()
        => _navigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            { nameof(Collection.ModeLocal), null },
            { nameof(Collection.ShortlistedUserId), null },
            { nameof(Collection.LikeCategoryId), null },
        });

    public string GetExternalNavigationLink(int songId, int versionId, string? externalId, SongsMode? mode = null)
    {
        return $"/collection?{nameof(Collection.SongId)}={songId}&{nameof(Collection.VersionId)}={versionId}&isopen=true&isopen2=true&{nameof(Collection.ExternalId)}={(externalId == null ? null : Uri.EscapeDataString(externalId))}{(mode == null ? null : $"&{nameof(Collection.ModeLocal)}={mode}")}";
    }

    public string GetExternalIllustrationsNavigationLink(int songId)
    {
        return $"/collection?{nameof(Collection.ModeLocal)}={SongsMode.WithIllustrations}&{nameof(Collection.SongId)}={songId}&isopen=true&isopen2=true";
    }

    public void Synced()
    {
        if (!_navigationManager.Uri.Contains($"{nameof(Collection.IsOpen2)}=true",
                StringComparison.InvariantCultureIgnoreCase))
        {
            _navigationManager.NavigateTo($"/collection?{nameof(Collection.ModeLocal)}={SongsMode.VersionCreationDesc}");
        }
    }
}