using System.Globalization;
using HarmonyDB.Index.Analysis;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Playground.Web.Identity;
using HarmonyDB.Playground.Web.Models;
using HarmonyDB.Playground.Web.Services;
using HarmonyDB.Source.Api.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HarmonyDB.Playground.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddJsonFile("appsettings.Secrets.json", true);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services
                .AddControllersWithViews(o => 
                    o.Filters.Add<MiddlewareFilterAttribute<LocalizationPipeline>>())
                .AddViewLocalization()
                .AddDataAnnotationsLocalization();

            builder.Services
                .AddLocalization()
                .AddIndexApiClient(builder.Configuration)
                .AddSourceApiClient(builder.Configuration)
                .AddIndexAnalysis()
                .AddScoped<Limiter>()
                .Configure<PlaygroundOptions>(o => builder.Configuration.GetSection(nameof(PlaygroundOptions)).Bind(o));

            builder.Services.AddAuthentication().AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
                googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            });

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
                options.FallBackToParentUICultures = true;

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
                pattern: "{ui-culture?}/{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            app.Run();
        }
    }
}
