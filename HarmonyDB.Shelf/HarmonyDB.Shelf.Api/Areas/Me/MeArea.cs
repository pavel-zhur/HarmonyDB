using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Auth;
using HarmonyDB.Shelf.Api.Areas.Auth.Services;
using HarmonyDB.Shelf.Api.Areas.Me.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Model.Auth;
using HarmonyDB.Shelf.Model.Me;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HarmonyDB.Shelf.Api.Areas.Me;

/// <summary>
/// The signed-in person's own: profile, settings, sessions, sign-ins, music links; wiping and deleting the profile.
/// Every endpoint is for the person's own session.
/// </summary>
public sealed class MeArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProfileService>();
        services.AddScoped<ProfileRemoval>();
        services.AddScoped<MusicLinkStore>();
        services.AddSingleton<MusicLinkCipher>();
        services.AddAudioClient(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/me").WithTags("Me").RequireSignIn();

        group.MapGet("", (ClaimsPrincipal user, ProfileService profile, PermissionCatalog permissions, CancellationToken cancellationToken)
            => profile.GetAsync(Me(user), AuthArea.BuiltIn(permissions.InForce(user)), cancellationToken)).WithName("GetProfile");

        group.MapGet("/settings", (ClaimsPrincipal user, ProfileService profile, CancellationToken cancellationToken)
            => profile.GetSettingsAsync(Me(user), cancellationToken)).WithName("GetSettings");

        group.MapPut("/settings", (ClaimsPrincipal user, Settings settings, ProfileService profile, CancellationToken cancellationToken)
            => profile.PutSettingsAsync(Me(user), settings, cancellationToken)).WithName("PutSettings");

        group.MapGet("/sessions", GetSessions).WithName("GetSessions");

        group.MapDelete("/sessions/{id:guid}", async (HttpContext context, Guid id, SessionService sessions, CancellationToken cancellationToken) =>
        {
            if (!await sessions.RevokeAsync(Me(context.User), id, cancellationToken))
                throw ShelfException.NotFound(MeErrorCodes.NoSuchSession);
            if (id == SessionService.GetSessionId(context.User))
                sessions.ForgetInBrowser(context);

            return TypedResults.NoContent();
        }).WithName("RevokeSession");

        // Every session but this one.
        group.MapDelete("/sessions", async (ClaimsPrincipal user, SessionService sessions, CancellationToken cancellationToken) =>
        {
            await sessions.RevokeOthersAsync(Me(user), SessionService.GetSessionId(user)!.Value, cancellationToken);
            return TypedResults.NoContent();
        }).WithName("RevokeOtherSessions");

        group.MapGet("/logins", (ClaimsPrincipal user, ProfileService profile, CancellationToken cancellationToken)
            => profile.ListLoginsAsync(Me(user), cancellationToken)).WithName("GetLogins");

        group.MapDelete("/logins/{id:guid}", async (
            ClaimsPrincipal user, Guid id, ProfileService profile, IOptionsMonitor<SignInOptions> signIn, TimeProvider time, CancellationToken cancellationToken) =>
        {
            // A session taken over must not remove the person's other sign-ins: a recent sign-in is needed.
            if (!SessionService.IsSignedInSince(user, time.GetUtcNow() - signIn.CurrentValue.FreshSignIn))
                throw ShelfException.Forbidden(AuthErrorCodes.FreshSignInRequired);

            await profile.RemoveLoginAsync(Me(user), id, cancellationToken);
            return TypedResults.NoContent();
        }).WithName("RemoveLogin");

        group.MapGet("/music-links", (ClaimsPrincipal user, MusicLinkStore links, CancellationToken cancellationToken)
            => links.ListAsync(Me(user), cancellationToken)).WithName("GetMusicLinks");

        // {service}: "yandex" or "youtube", as the JSON writes MusicService.
        group.MapPut("/music-links/{service}", (ClaimsPrincipal user, string service, PutMusicLinkRequest request, MusicLinkStore links, CancellationToken cancellationToken)
            => links.PutAsync(Me(user), ParseService(service), request.Credentials, cancellationToken)).WithName("PutMusicLink");

        group.MapDelete("/music-links/{service}", async (ClaimsPrincipal user, string service, MusicLinkStore links, CancellationToken cancellationToken) =>
        {
            await links.DeleteAsync(Me(user), ParseService(service), cancellationToken);
            return TypedResults.NoContent();
        }).WithName("DeleteMusicLink");

        // Songs, playlists, marks … go; the profile stays.
        group.MapPost("/wipe", async (ClaimsPrincipal user, ProfileRemoval removal, CancellationToken cancellationToken) =>
        {
            await removal.WipeAsync(Me(user), cancellationToken);
            return TypedResults.NoContent();
        }).WithName("WipeProfile");

        group.MapDelete("", async (HttpContext context, ProfileRemoval removal, SessionService sessions, CancellationToken cancellationToken) =>
        {
            await removal.DeleteAsync(Me(context.User), cancellationToken);
            sessions.ForgetInBrowser(context);
            return TypedResults.NoContent();
        }).WithName("DeleteProfile");
    }

    private static async Task<Ok<List<SessionItem>>> GetSessions(ClaimsPrincipal user, SessionService sessions, CancellationToken cancellationToken)
    {
        var current = SessionService.GetSessionId(user);
        var list = await sessions.ListAsync(Me(user), cancellationToken);
        return TypedResults.Ok(list.Select(x => new SessionItem
        {
            Id = x.Id,
            Current = x.Id == current,
            CreatedAt = x.CreatedAt,
            SignedInAt = x.SignedInAt,
            LastSeenAt = x.LastSeenAt,
            ExpiresAt = x.ExpiresAt,
            UserAgent = x.UserAgent,
            IpAddress = x.IpAddress,
        }).ToList());
    }

    private static MusicService ParseService(string service) => service switch
    {
        "yandex" => MusicService.Yandex,
        "youtube" => MusicService.Youtube,
        _ => throw ShelfException.NotFound(MeErrorCodes.NoSuchMusicLink),
    };

    /// <summary>
    /// The signed-in person (the group requires sign-in).
    /// </summary>
    private static Guid Me(ClaimsPrincipal user) => user.GetAccountId()!.Value;
}
