using OneShelf.Common.Songs.FullTextSearch;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Model.V3.Databasish;
using OneShelf.Frontend.Api.Model.V3.Databasish.Interfaces;
using Version = OneShelf.Frontend.Api.Model.V3.Databasish.Version;

namespace OneShelf.Frontend.Web.Models;

public class CollectionIndex
{
    private readonly long _userId;
    private readonly IReadOnlyCollection<(ISong song, IReadOnlyCollection<string> words)> _cache;

    public CollectionIndex(ICollection immutableCollection, long userId, ApplyRequest applyRequest)
    {
        _userId = userId;

        var collection = immutableCollection.Clone();

        ArtistsById = collection.Artists.ToDictionary(x => x.Id);
        UsersById = collection.Users.ToDictionary(x => x.Id);
        SongsById = collection.Songs.ToDictionary(x => x.Id, x => (ISong)x);
        LikeCategoriesById = collection.LikeCategories.ToDictionary(x => x.Id);

        var existingExternalIds = collection.Songs.SelectMany(x => x.Versions.Select(u => (x, u)))
            .Where(x => x.u.ExternalId != null)
            .Select(x => x.u.ExternalId!)
            .ToHashSet();

        ExistingExternalIdsWithVirtual = existingExternalIds;

        VirtualLikeLevels = new();

        foreach (var updatedLike in applyRequest.UpdatedLikes.OrderBy(x => x.HappenedOn))
        {
            if (!SongsById.TryGetValue(updatedLike.SongId, out var immutableSong)) continue;

            var song = immutableSong.Clone();

            var existing = song.Likes.FirstOrDefault(x => 
                x.VersionId == updatedLike.VersionId 
                && (x.UserId == userId || x.LikeCategoryId.HasValue && LikeCategoriesById[x.LikeCategoryId.Value].Access == LikeCategoryAccess.SharedEditSingle) 
                && x.LikeCategoryId == updatedLike.LikeCategoryId);
            if (updatedLike.Level.HasValue)
            {
                var like = new Like
                {
                    CreatedOn = existing?.CreatedOn ?? updatedLike.HappenedOn,
                    VersionId = updatedLike.VersionId,
                    UserId = userId,
                    Level = updatedLike.Level.Value,
                    Transpose = updatedLike.Transpose,
                    LikeCategoryId = updatedLike.LikeCategoryId,
                };

                if (existing != null)
                {
                    song.Likes[song.Likes.IndexOf(existing)] = like;
                }
                else
                {
                    song.Likes.Add(like);
                }
            }
            else if (existing != null)
            {
                song.Likes.Remove(existing);
            }

            SongsById[song.Id] = song;
            collection.Songs[collection.Songs.IndexOf((Song)immutableSong)] = song;
            collection.Etag = null;
        }

        var versionsById = collection.Songs.SelectMany(x => x.Versions.Select(v => (x, v)))
            .ToDictionary(x => x.v.Id, x => (x.v, s: (ISong)x.x));

        foreach (var removedVersion in applyRequest.RemovedVersions)
        {
            if (!versionsById.TryGetValue(removedVersion.VersionId, out var immutableSong)) continue;

            var version = immutableSong.v;
            if (version.UserId != userId) continue;

            var song = immutableSong.s.Clone();
            song.Versions.Remove(version);
            song.Likes.RemoveAll(x => x.VersionId == removedVersion.VersionId);

            SongsById[song.Id] = song;
            collection.Songs[collection.Songs.IndexOf((Song)immutableSong.s)] = song;
            collection.Etag = null;

            versionsById.Remove(version.Id);
        }

        foreach (var importedVersion in applyRequest.ImportedVersions)
        {
            existingExternalIds.Add(importedVersion.ExternalId);
            VirtualLikeLevels[importedVersion.ExternalId] = (importedVersion.Level, importedVersion.LikeCategoryId);
        }

        VersionsByUri = collection.Songs.SelectMany(x => x.Versions.Select(v => (x, v)))
            .ToLookup(x => x.v.Uri, x => (x.v, (ISong)x.x));

        VersionsByExternalId = collection.Songs.SelectMany(x => x.Versions.Select(v => (x, v)))
            .Where(x => x.v.ExternalId != null)
            .ToLookup(x => x.v.ExternalId!, x => (x.v, (ISong)x.x));

        VersionsById = versionsById;

        Collection = collection;
        _cache = FullTextSearchLogic.BuildCache(collection.Songs.Cast<ISong>(), x => x.Artists.Select(x => ArtistsById[x]));
    }

    public ICollection Collection { get; set; }

    public Dictionary<long, User> UsersById { get; set; }

    public Dictionary<int, Artist> ArtistsById { get; set; }
    
    public Dictionary<int, ISong> SongsById { get; set; }

    public IReadOnlyCollection<string> ExistingExternalIdsWithVirtual { get; set; }

    public ILookup<Uri, (Version version, ISong song)> VersionsByUri { get; set; }

    public ILookup<string, (Version version, ISong song)> VersionsByExternalId { get; set; }

    public IReadOnlyDictionary<int, (Version version, ISong song)> VersionsById { get; set; }

    public Dictionary<string, (byte level, int? likeCategoryId)> VirtualLikeLevels { get; set; }

    public Dictionary<int, LikeCategory> LikeCategoriesById { get; set; }

    public List<ISong> Find(string query)
    {
        return FullTextSearchLogic.Find(_cache, query, _userId).found;
    }

    public bool DeepEquals(CollectionIndex another)
    {
        return another.SongsById.Keys.OrderBy(x => x).SequenceEqual(SongsById.Keys.OrderBy(x => x))
               && another.ArtistsById.Keys.OrderBy(x => x).SequenceEqual(ArtistsById.Keys.OrderBy(x => x))
               && another.UsersById.Keys.OrderBy(x => x).SequenceEqual(UsersById.Keys.OrderBy(x => x))
               && another.ExistingExternalIdsWithVirtual.SequenceEqual(ExistingExternalIdsWithVirtual)
               && Collection == another.Collection || Collection.DeepHash().SequenceEqual(another.Collection.DeepHash());
    }
}