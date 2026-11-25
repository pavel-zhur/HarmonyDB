namespace HarmonyDB.Shelf.Api.Foundation.Areas;

public static class AreaExtensions
{
    /// <summary>
    /// Adds an area: its services now, its endpoints at <see cref="MapAreas"/>.
    /// </summary>
    public static WebApplicationBuilder AddArea<TArea>(this WebApplicationBuilder builder)
        where TArea : IShelfArea, new()
    {
        var area = new TArea();
        area.AddServices(builder.Services, builder.Configuration);
        builder.Services.AddSingleton<IShelfArea>(area);
        return builder;
    }

    /// <summary>
    /// Maps the endpoints of every area added.
    /// </summary>
    public static WebApplication MapAreas(this WebApplication app)
    {
        foreach (var area in app.Services.GetServices<IShelfArea>())
        {
            area.MapEndpoints(app);
        }

        return app;
    }
}
