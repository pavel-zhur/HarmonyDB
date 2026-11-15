using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OneShelf.Common.Api.Hosting;

// How the functions of a module are found, bound and guarded: the parts of ASP.NET Core MVC set up to behave as
// the Azure Functions HTTP triggers did.

/// <summary>
/// The keys of a module's functions of the <see cref="AuthorizationLevel.Function"/> level (setting <c>FunctionKeys</c>, a list).
/// </summary>
public class FunctionKeysOptions
{
    public const string SectionName = "FunctionKeys";

    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// The classes of the module's assembly that have functions are its controllers.
/// </summary>
internal class FunctionControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var type in parts.OfType<IApplicationPartTypeProvider>().SelectMany(x => x.Types))
        {
            if (type is { IsClass: true, IsPublic: true, IsAbstract: false, ContainsGenericParameters: false }
                && type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(x => x.GetCustomAttribute<FunctionAttribute>() != null)
                && !feature.Controllers.Contains(type))
            {
                feature.Controllers.Add(type);
            }
        }
    }
}

/// <summary>
/// Only the methods marked as functions are endpoints.
/// </summary>
internal class FunctionsOnlyConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        foreach (var action in controller.Actions.Where(x => !x.Attributes.OfType<FunctionAttribute>().Any()).ToList())
        {
            controller.Actions.Remove(action);
        }
    }
}

/// <summary>
/// A function of the <see cref="AuthorizationLevel.Function"/> level answers 401 with no body unless one of the keys is
/// presented in the "code" query parameter or the "x-functions-key" header.
/// </summary>
internal class FunctionKeysFilter : IAuthorizationFilter
{
    private const string QueryParameter = "code";
    private const string Header = "x-functions-key";

    private readonly IReadOnlyList<byte[]> _keys;

    public FunctionKeysFilter(IOptions<FunctionKeysOptions> options)
    {
        _keys = options.Value.Keys.Where(x => !string.IsNullOrEmpty(x)).Select(Encoding.UTF8.GetBytes).ToList();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var level = context.ActionDescriptor.EndpointMetadata.OfType<FunctionAttribute>().Single().AuthorizationLevel;
        if (level == AuthorizationLevel.Anonymous) return;

        var request = context.HttpContext.Request;
        var presented = request.Headers[Header].FirstOrDefault() ?? request.Query[QueryParameter].FirstOrDefault();
        if (presented == null || !_keys.Any(key => CryptographicOperations.FixedTimeEquals(key, Encoding.UTF8.GetBytes(presented))))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}

/// <summary>
/// A request that cannot be bound (no body, a body that is not JSON, an unsupported content type) is answered with
/// 500 without running the function, as the Azure Functions binding failed.
/// </summary>
internal class BindingFailureFilter : IActionFilter
{
    private readonly ILogger<BindingFailureFilter> _logger;

    public BindingFailureFilter(ILogger<BindingFailureFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;

        _logger.LogError("The request could not be bound: {errors}",
            string.Join("; ", context.ModelState.SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.Exception?.Message ?? e.ErrorMessage}"))));
        context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

/// <summary>
/// A parameter of the type <see cref="HttpRequest"/> gets the request.
/// </summary>
internal class HttpRequestModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
        => context.Metadata.ModelType == typeof(HttpRequest) ? new HttpRequestModelBinder() : null;

    private class HttpRequestModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            bindingContext.Result = ModelBindingResult.Success(bindingContext.HttpContext.Request);
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// The bound requests are not validated, as the Azure Functions did not validate them.
/// </summary>
internal class NoObjectModelValidator : IObjectModelValidator
{
    public void Validate(ActionContext actionContext, ValidationStateDictionary? validationState, string prefix, object? model)
    {
    }
}
