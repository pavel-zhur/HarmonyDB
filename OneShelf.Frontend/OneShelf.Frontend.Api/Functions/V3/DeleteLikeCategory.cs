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
using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Api.Functions.V3;

public class DeleteLikeCategory : AuthorizationFunctionBase<DeleteLikeCategoryRequest, DeleteLikeCategoryResponse>
{
    private readonly SongsDatabase _songsDatabase;

    public DeleteLikeCategory(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, AuthorizationApiClient authorizationApiClient)
        : base(loggerFactory, authorizationApiClient)
    {
        _songsDatabase = songsDatabase;
    }

    [Function(ApiUrls.V3DeleteLikeCategory)]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] DeleteLikeCategoryRequest request) => await RunHandler(req, request);

    protected override async Task<DeleteLikeCategoryResponse> Execute(HttpRequest httpRequest,
        DeleteLikeCategoryRequest request)
    {
        var likeCategory = await _songsDatabase.LikeCategories
            .Include(x => x.Likes)
            .SingleOrDefaultAsync(x => x.Id == request.LikeCategoryId);

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

        _songsDatabase.Likes.RemoveRange(likeCategory.Likes);
        await _songsDatabase.SaveChangesAsyncX();

        _songsDatabase.LikeCategories.Remove(likeCategory);
        await _songsDatabase.SaveChangesAsyncX();

        return new()
        {
            Result = true,
        };
    }
}