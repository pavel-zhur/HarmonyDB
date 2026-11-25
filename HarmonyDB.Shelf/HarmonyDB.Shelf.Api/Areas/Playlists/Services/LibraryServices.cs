using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Library.Services;
using HarmonyDB.Shelf.Api.Areas.Share.Services;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Items;
using HarmonyDB.Shelf.Api.Common.Jobs;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// The services of the areas of playlists, the library, links and the sync, which use one another; each of these areas
/// adds them all (once).
/// </summary>
public static class LibraryServices
{
    public static void Add(IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(x => x.ServiceType == typeof(PlaylistGate)))
            return;

        services.AddItemRows();
        services.AddChordsReading(configuration);
        services.AddShelfCommon();
        services.AddAudioClient(configuration);
        services.AddJobHandler<OriginSyncHandler>(configuration, PlaylistService.SyncJobKind);
        services.AddOptions<PlaylistsOptions>().Bind(configuration.GetSection(PlaylistsOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PlaylistsOptions>, PlaylistsOptionsValidator>());
        services.AddScoped<ChordsLookup>();
        services.AddScoped<PlaylistGate>();
        services.AddScoped<Views>();
        services.AddScoped<UndoTokens>();
        services.AddScoped<PlaylistService>();
        services.AddScoped<SongService>();
        services.AddScoped<SongLists>();
        services.AddScoped<SongPages>();
        services.AddScoped<OriginSync>();
        services.AddScoped<RecentService>();
        services.AddScoped<ShareService>();
        services.AddScoped<SyncService>();
    }
}
