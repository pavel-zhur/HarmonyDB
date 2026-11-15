using Microsoft.AspNetCore.Builder;

namespace OneShelf.Common.Api.Hosting;

/// <summary>
/// An API application that runs inside a host together with others, as if it ran alone: it has its own services,
/// settings, logging and background work, and its functions (see <see cref="FunctionAttribute"/>) are served under its
/// own path base. Modules call each other over HTTP, through their clients, as separate applications do.
/// </summary>
public abstract class ApiModule
{
    protected ApiModule(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The path base of the module is this name in lower case (<c>/{name}/api/...</c>). The section of the host's
    /// settings with this name is laid over the host's settings for the module.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// What the module's Program did: registers its services and adjusts its host.
    /// <paramref name="builder"/>'s configuration is the module's.
    /// </summary>
    public abstract void Configure(WebApplicationBuilder builder);
}
