using System.Reflection;
using HarmonyDB.Shelf.Model.App;

namespace HarmonyDB.Shelf.Api.Areas.App.Services;

public sealed class VersionService
{
    private readonly VersionResponse _version = new()
    {
        Version = typeof(VersionService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    };

    public VersionResponse Get() => _version;
}
