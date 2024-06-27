using System.Globalization;
using HarmonyDB.Index.Analysis;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Client;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Playground.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services
                .AddControllersWithViews(o => 
                    o.Filters.Add<MiddlewareFilterAttribute<LocalizationPipeline>>())
                .AddViewLocalization()
                .AddDataAnnotationsLocalization();

            builder.Services
                .AddLocalization()
                .AddIndexApiClient(builder.Configuration)
                .AddSourceApiClient(builder.Configuration)
                .AddIndexAnalysis();

            const string defaultCulture = "en";

            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo(defaultCulture),
                    new CultureInfo("ru"),
                };

                options.DefaultRequestCulture = new(defaultCulture);
                options.SupportedUICultures = supportedCultures;

                options.RequestCultureProviders.Clear();
                options.RequestCultureProviders.Add(new RouteDataRequestCultureProvider
                {
                    Options = options,
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: $"{{culture={defaultCulture}}}/{{controller=Home}}/{{action=Index}}/{{id?}}");

            app.Run();
        }
    }
}
