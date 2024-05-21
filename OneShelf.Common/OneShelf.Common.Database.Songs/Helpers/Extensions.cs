using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;

namespace OneShelf.Common.Database.Songs.Helpers;

public static class Extensions
{
    public static SongCategory GetSongCategory(this Artist artist) => artist.CategoryOverride ?? ("йцукенгшщзхъфывапролджэячсмитьбюё".Any(artist.Name.Contains) ? SongCategory.Domestic : SongCategory.Foreign);
}