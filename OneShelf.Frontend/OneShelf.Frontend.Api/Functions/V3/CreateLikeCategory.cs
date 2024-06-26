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
using OneShelf.Frontend.Api.Model.V3;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Tools;
using LikeCategory = OneShelf.Common.Database.Songs.Model.LikeCategory;

namespace OneShelf.Frontend.Api.Functions.V3;

public class CreateLikeCategory : AuthorizationFunctionBase<CreateLikeCategoryRequest, CreateLikeCategoryResponse>
{
    private readonly SongsDatabase _songsDatabase;

    public CreateLikeCategory(ILoggerFactory loggerFactory, SongsDatabase songsDatabase, AuthorizationApiClient authorizationApiClient, SecurityContext securityContext)
        : base(loggerFactory, authorizationApiClient, securityContext)
    {
        _songsDatabase = songsDatabase;
    }

    [Function(ApiUrls.V3CreateLikeCategory)]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] CreateLikeCategoryRequest request) => await RunHandler(req, request);

    protected override async Task<CreateLikeCategoryResponse> Execute(HttpRequest httpRequest,
        CreateLikeCategoryRequest request)
    {
        if (request.LikeCategory.UserId != request.Identity.Id)
        {
            throw new("Bad identity vs user id.");
        }

        LikeCategoriesTools.Validate(request.LikeCategory);

        if (await _songsDatabase.LikeCategories.CountAsync(c => c.UserId == request.Identity.Id) > Constants.TotalLikeCategories)
        {
            return new()
            {
                Result = false,
            };
        }

        var likeCategory = new LikeCategory
        {
            TenantId = SecurityContext.TenantId,
            Name = request.LikeCategory.Name,
            Access = request.LikeCategory.Access.ToDatabaseAccess(),
            CssColor = "any",
            CssIcon = "any",
            Order = request.LikeCategory.Order,
            PrivateWeight = Constants.DefaultLikeCategoryPrivateWeight,
            UserId = request.Identity.Id,
        };

        _songsDatabase.LikeCategories.Add(likeCategory);
        await _songsDatabase.SaveChangesAsyncX();

        return new()
        {
            Result = true,
        };
    }
}