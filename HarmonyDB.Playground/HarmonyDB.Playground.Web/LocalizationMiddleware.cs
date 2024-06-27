using System.Globalization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Playground.Web;

public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _routeDataStringKey;

    public LocalizationMiddleware(
        RequestDelegate next,
        IOptions<RequestLocalizationOptions> options)
    {
        _next = next;

        var provider = options.Value.RequestCultureProviders
            .OfType<RouteDataRequestCultureProvider>()
            .Single();

        _routeDataStringKey = provider.UIRouteDataStringKey;
    }

    public async Task Invoke(HttpContext context)
    {
        var urlRequestedCulture = context.GetRouteValue(_routeDataStringKey)?.ToString();
        var correctCulture = CultureInfo.CurrentUICulture.Name;

        if (urlRequestedCulture == null)
        {
            context.GetRouteData().Values[_routeDataStringKey] = correctCulture;
        }
        else if (urlRequestedCulture != correctCulture)
        {
            var originalUrl = $"{context.Request.Path.Value}{context.Request.QueryString}";
            var value1 = $"/{urlRequestedCulture}";
            var value2 = $"/{urlRequestedCulture}/";
            string? redirectTo = null;
            if (originalUrl == value1)
            {
                redirectTo = $"/{correctCulture}";
            }
            else if (originalUrl.StartsWith(value2))
            {
                redirectTo = $"/{correctCulture}/{originalUrl.Substring(value2.Length)}";
            }

            if (redirectTo != null)
            {
                context.Response.Redirect(redirectTo);
                return;
            }

            context.RequestServices.GetRequiredService<ILogger<LocalizationPipeline>>().LogError("Could not redirect to fix the culture. {data}", (urlRequestedCulture, correctCulture, originalUrl));
        }

        await _next.Invoke(context);
    }
}