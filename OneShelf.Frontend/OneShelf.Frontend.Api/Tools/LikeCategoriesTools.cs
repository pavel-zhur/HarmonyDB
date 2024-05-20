using OneShelf.Frontend.Api.Model.V3;
using OneShelf.Frontend.Api.Model.V3.Databasish;

namespace OneShelf.Frontend.Api.Tools;

internal static class LikeCategoriesTools
{
    public static void Validate(LikeCategory likeCategory)
    {
        if (likeCategory.Name.Length > Constants.MaxLikeCategoryNameLength)
        {
            throw new("Name too long.");
        }

        if (string.IsNullOrWhiteSpace(likeCategory.Name))
        {
            throw new("Name empty.");
        }

        if (likeCategory.Name.Trim() != likeCategory.Name)
        {
            throw new("Name not trimmed.");
        }

        if (!Enum.GetValues<LikeCategoryAccess>().Contains(likeCategory.Access))
        {
            throw new("Unexpected access.");
        }
    }

    public static Common.Database.Songs.Model.Enums.LikeCategoryAccess ToDatabaseAccess(this LikeCategoryAccess likeCategoryAccess)
    {
        return likeCategoryAccess switch
        {
            LikeCategoryAccess.Private => Common.Database.Songs.Model.Enums.LikeCategoryAccess.Private,
            LikeCategoryAccess.SharedView => Common.Database.Songs.Model.Enums.LikeCategoryAccess.SharedView,
            LikeCategoryAccess.SharedEditMulti => Common.Database.Songs.Model.Enums.LikeCategoryAccess.SharedEditMulti,
            LikeCategoryAccess.SharedEditSingle => Common.Database.Songs.Model.Enums.LikeCategoryAccess.SharedEditSingle,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}