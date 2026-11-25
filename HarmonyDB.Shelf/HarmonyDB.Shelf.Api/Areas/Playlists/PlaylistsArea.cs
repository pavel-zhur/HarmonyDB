using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists;

/// <summary>
/// Playlists: the person's and the public ones; a playlist's page, settings, members, invitations, deleting, leaving;
/// its songs (a page of the list, a song's page), adding, renaming, moving, removing, variants, linking and unlinking,
/// one's main variant; a music service's playlist imported by its link and its sync. Reading is open to whoever sees the
/// playlist (with <c>?share=</c>, the token of a link they opened); every change is the person's own session's, and
/// answers with its undo.
/// </summary>
public sealed class PlaylistsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => LibraryServices.Add(services, configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/playlists").WithTags("Playlists");

        group.MapGet("", (ClaimsPrincipal user, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.ListMineAsync(Me(user), cancellationToken)).RequireSignIn().WithName("GetMyPlaylists");

        group.MapGet("/public", (ClaimsPrincipal user, string? q, int? limit, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.ListPublicAsync(user.GetAccountId(), q, limit ?? 50, cancellationToken)).WithName("GetPublicPlaylists");

        group.MapPost("", (ClaimsPrincipal user, CreatePlaylistRequest request, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.CreateAsync(Me(user), request, cancellationToken)).RequireSignIn().WithName("CreatePlaylist");

        group.MapPost("/import", (ClaimsPrincipal user, ImportPlaylistRequest request, OriginSync origin, CancellationToken cancellationToken)
            => origin.ImportAsync(Me(user), request.Url, cancellationToken)).RequireSignIn().WithName("ImportPlaylist");

        group.MapGet("/{id:guid}", (ClaimsPrincipal user, Guid id, string? share, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.GetAsync(user.GetAccountId(), id, share, cancellationToken)).WithName("GetPlaylist");

        group.MapPatch("/{id:guid}", (ClaimsPrincipal user, Guid id, UpdatePlaylistRequest request, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.UpdateAsync(Me(user), id, request, cancellationToken)).RequireSignIn().WithName("UpdatePlaylist");

        group.MapDelete("/{id:guid}", (ClaimsPrincipal user, Guid id, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.DeleteAsync(Me(user), id, cancellationToken)).RequireSignIn().WithName("DeletePlaylist");

        group.MapPut("/{id:guid}/library", (ClaimsPrincipal user, Guid id, ShowInLibraryRequest request, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.ShowInLibraryAsync(Me(user), id, request.Show, cancellationToken)).RequireSignIn().WithName("ShowPlaylistInLibrary");

        group.MapPost("/{id:guid}/leave", (ClaimsPrincipal user, Guid id, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.LeaveAsync(Me(user), id, cancellationToken)).RequireSignIn().WithName("LeavePlaylist");

        // what is mine in the playlist: my "here" keys and notes in it, and (?added=true) what I added to it
        group.MapDelete("/{id:guid}/mine", (ClaimsPrincipal user, Guid id, bool? added, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.ClearMineAsync(Me(user), id, added ?? false, cancellationToken)).RequireSignIn().WithName("ClearMineInPlaylist");

        group.MapPut("/{id:guid}/members/{accountId:guid}", (ClaimsPrincipal user, Guid id, Guid accountId, SetMemberRoleRequest request, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.SetRoleAsync(Me(user), id, accountId, request.Role, cancellationToken)).RequireSignIn().WithName("SetMemberRole");

        group.MapDelete("/{id:guid}/members/{accountId:guid}", (ClaimsPrincipal user, Guid id, Guid accountId, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.RemoveMemberAsync(Me(user), id, accountId, cancellationToken)).RequireSignIn().WithName("RemoveMember");

        group.MapPost("/{id:guid}/owner", (ClaimsPrincipal user, Guid id, PassOwnershipRequest request, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.PassOwnershipAsync(Me(user), id, request.AccountId, cancellationToken)).RequireSignIn().WithName("PassOwnership");

        group.MapPost("/{id:guid}/invitations", (ClaimsPrincipal user, Guid id, CreateInvitationRequest request, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.CreateInvitationAsync(Me(user), id, request, cancellationToken)).RequireSignIn().WithName("CreateInvitation");

        group.MapDelete("/{id:guid}/invitations/{token}", (ClaimsPrincipal user, Guid id, string token, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.RevokeInvitationAsync(Me(user), id, token, cancellationToken)).RequireSignIn().WithName("RevokeInvitation");

        group.MapGet("/invitations/{token}", (ClaimsPrincipal user, string token, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.GetInvitationAsync(user.GetAccountId(), token, cancellationToken)).WithName("GetInvitation");

        group.MapPost("/invitations/{token}/accept", (ClaimsPrincipal user, string token, PlaylistService playlists, CancellationToken cancellationToken)
            => playlists.AcceptInvitationAsync(Me(user), token, cancellationToken)).RequireSignIn().WithName("AcceptInvitation");

        group.MapPost("/{id:guid}/sync", (ClaimsPrincipal user, Guid id, OriginSync origin, CancellationToken cancellationToken)
            => origin.StartAsync(Me(user), id, cancellationToken)).RequireSignIn().WithName("SyncPlaylist");

        group.MapGet("/{id:guid}/songs", (
                ClaimsPrincipal user, Guid id, string? share, string? q, string? sort, string? who, string? mark, int? offset, int? limit,
                SongLists lists, CancellationToken cancellationToken)
            => lists.PlaylistAsync(user, id, share, new(q, SongListQuery.ParseSort(sort), who, mark, offset ?? 0, limit ?? 200), cancellationToken)).WithName("GetPlaylistSongs");

        group.MapPost("/{id:guid}/songs", (ClaimsPrincipal user, Guid id, AddSongRequest request, SongService songs, CancellationToken cancellationToken)
            => songs.AddAsync(Me(user), id, request, cancellationToken)).RequireSignIn().WithName("AddSong");

        group.MapGet("/{id:guid}/songs/{songId:guid}", (ClaimsPrincipal user, Guid id, Guid songId, string? share, SongPages pages, CancellationToken cancellationToken)
            => pages.GetAsync(user, id, songId, share, cancellationToken)).WithName("GetSong");

        group.MapPatch("/{id:guid}/songs/{songId:guid}", (ClaimsPrincipal user, Guid id, Guid songId, UpdateSongRequest request, SongService songs, CancellationToken cancellationToken)
            => songs.UpdateAsync(Me(user), id, songId, request, cancellationToken)).RequireSignIn().WithName("UpdateSong");

        group.MapDelete("/{id:guid}/songs/{songId:guid}", (ClaimsPrincipal user, Guid id, Guid songId, SongService songs, CancellationToken cancellationToken)
            => songs.RemoveAsync(Me(user), id, songId, cancellationToken)).RequireSignIn().WithName("RemoveSong");

        group.MapPost("/{id:guid}/songs/{songId:guid}/variants", (ClaimsPrincipal user, Guid id, Guid songId, AddVariantRequest request, SongService songs, CancellationToken cancellationToken)
            => songs.AddVariantAsync(Me(user), id, songId, request, cancellationToken)).RequireSignIn().WithName("AddVariant");

        group.MapDelete("/{id:guid}/songs/{songId:guid}/variants/{itemId:guid}", (ClaimsPrincipal user, Guid id, Guid songId, Guid itemId, SongService songs, CancellationToken cancellationToken)
            => songs.RemoveVariantAsync(Me(user), id, songId, itemId, cancellationToken)).RequireSignIn().WithName("RemoveVariant");

        group.MapPost("/{id:guid}/songs/{songId:guid}/link", (ClaimsPrincipal user, Guid id, Guid songId, LinkSongsRequest request, SongService songs, CancellationToken cancellationToken)
            => songs.LinkAsync(Me(user), id, songId, request, cancellationToken)).RequireSignIn().WithName("LinkSongs");

        group.MapPost("/{id:guid}/songs/{songId:guid}/variants/{itemId:guid}/unlink", (ClaimsPrincipal user, Guid id, Guid songId, Guid itemId, SongService songs, CancellationToken cancellationToken)
            => songs.UnlinkAsync(Me(user), id, songId, itemId, cancellationToken)).RequireSignIn().WithName("UnlinkVariant");

        group.MapPut("/{id:guid}/songs/{songId:guid}/main", (ClaimsPrincipal user, Guid id, Guid songId, string? share, SetMainVariantRequest request, SongService songs, CancellationToken cancellationToken)
            => songs.SetMainAsync(Me(user), id, songId, request.ItemId, share, cancellationToken)).RequireSignIn().WithName("SetMainVariant");
    }

    /// <summary>
    /// The signed-in person (the endpoint requires sign-in).
    /// </summary>
    private static Guid Me(ClaimsPrincipal user) => user.GetAccountId()!.Value;
}
