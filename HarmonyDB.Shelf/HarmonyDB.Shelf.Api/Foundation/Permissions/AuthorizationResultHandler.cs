using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace HarmonyDB.Shelf.Api.Foundation.Permissions;

/// <summary>
/// Answers a failed authorization with its code: 401 <c>sign_in_required</c> when the caller is not signed in;
/// 403 <c>permission_denied</c> when a permission is missing; 403 <c>forbidden</c> otherwise.
/// </summary>
internal sealed class AuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
            return next(context);

        if (authorizeResult.Challenged)
            return ErrorResponses.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.SignInRequired);

        var permissionMissing = authorizeResult.AuthorizationFailure?.FailedRequirements.Any(x => x is PermissionRequirement) == true;
        return ErrorResponses.WriteAsync(
            context,
            StatusCodes.Status403Forbidden,
            permissionMissing ? ErrorCodes.PermissionDenied : ErrorCodes.Forbidden);
    }
}
