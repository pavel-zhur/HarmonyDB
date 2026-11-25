using Microsoft.AspNetCore.Mvc.Routing;

namespace OneShelf.Common.Api;

/// <summary>
/// Makes a public method of a public class an HTTP endpoint of its module: <c>api/{name}</c> (or <c>api/{Route}</c>)
/// under the module's path base, answering the given methods, open to the callers of the given level.
/// The class is created for every call, its constructor's parameters taken from the module's services.
/// The method's parameters are bound as in ASP.NET Core MVC; an <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>
/// parameter gets the request itself.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FunctionAttribute : Attribute, IRouteTemplateProvider, IActionHttpMethodProvider
{
    public const string RoutePrefix = "api";

    private readonly string[] _methods;

    public FunctionAttribute(string name, AuthorizationLevel authorizationLevel, params string[] methods)
    {
        if (methods.Length == 0) throw new ArgumentException("At least one method is required.", nameof(methods));

        Name = name;
        AuthorizationLevel = authorizationLevel;
        _methods = methods.Select(x => x.ToUpperInvariant()).ToArray();
    }

    public string Name { get; }

    public AuthorizationLevel AuthorizationLevel { get; }

    /// <summary>
    /// The route after the prefix, when it is not the name, e.g. <c>images/{id:guid}/{size:int}</c>.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>
    /// The scope an internal client (see <c>HarmonyDB.Common.ServiceAuth</c>) needs to call the function with its key, in
    /// the <c>X-Internal-Key</c> header. Required for the <see cref="AuthorizationLevel.Internal"/> level; on the
    /// <see cref="AuthorizationLevel.Function"/> level it opens the function to such a client besides the function keys.
    /// </summary>
    public string? InternalScope { get; init; }

    public IEnumerable<string> HttpMethods => _methods;

    string IRouteTemplateProvider.Template => $"{RoutePrefix}/{Route ?? Name}";

    int? IRouteTemplateProvider.Order => null;

    string? IRouteTemplateProvider.Name => null;
}
