using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Tools;

namespace OneShelf.Frontend.Api.Functions.V3;

public class UpdateLikeCategory : AuthorizationFunctionBase<UpdateLikeCategoryRequest, UpdateLikeCategoryResponse>
{
    private readonly SongsDatabase _songsDatabase;

    public UpdateLikeCategory(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, AuthorizationApiClient authorizationApiClient)
        : base(loggerFactory, authorizationApiClient)
    {
        _songsDatabase = songsDatabase;
    }

    [Function(ApiUrls.V3UpdateLikeCategory)]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] UpdateLikeCategoryRequest request) => await RunHandler(req, request);

    protected override async Task<UpdateLikeCategoryResponse> Execute(HttpRequest httpRequest,
        UpdateLikeCategoryRequest request)
    {
        if (request.LikeCategory.UserId != request.Identity.Id)
        {
            throw new("Bad identity vs user id.");
        }

        LikeCategoriesTools.Validate(request.LikeCategory);

        var likeCategory = await _songsDatabase.LikeCategories.Include(x => x.Likes).SingleOrDefaultAsync(x => x.Id == request.LikeCategory.Id);
        if (likeCategory == null)
        {
            return new()
            {
                Result = false,
            };
        }

        if (likeCategory.UserId != request.Identity.Id)
        {
            throw new("Wrong identity.");
        }

        var newAccess = request.LikeCategory.Access.ToDatabaseAccess();
        var oldAccess = likeCategory.Access;
        if (oldAccess != newAccess)
        {
            if (oldAccess is LikeCategoryAccess.SharedEditMulti or LikeCategoryAccess.SharedEditSingle
                && newAccess is LikeCategoryAccess.Private or LikeCategoryAccess.SharedView)
            {
                foreach (var sameGroupLikes in likeCategory.Likes
                             .GroupBy(x => (x.SongId, x.VersionId)))
                {
                    var likes = sameGroupLikes.OrderBy(x => x.UserId == request.Identity.Id ? 1 : 2).ThenBy(x => x.CreatedOn).ToList();
                    _songsDatabase.Likes.RemoveRange(likes.Skip(1));
                    likes[0].UserId = request.Identity.Id;
                }

                await _songsDatabase.SaveChangesAsyncX();
            }

            if (oldAccess == LikeCategoryAccess.SharedEditMulti && newAccess == LikeCategoryAccess.SharedEditSingle)
            {
                foreach (var sameGroupLikes in likeCategory.Likes
                             .GroupBy(x => (x.SongId, x.VersionId)))
                {
                    var likes = sameGroupLikes.OrderBy(x => x.CreatedOn).ToList();
                    _songsDatabase.Likes.RemoveRange(likes.Skip(1));
                }

                await _songsDatabase.SaveChangesAsyncX();
            }
        }

        likeCategory.Name = request.LikeCategory.Name;
        likeCategory.Order = request.LikeCategory.Order;
        likeCategory.Access = newAccess;
        await _songsDatabase.SaveChangesAsyncX();

        return new()
        {
            Result = true,
        };
    }
}