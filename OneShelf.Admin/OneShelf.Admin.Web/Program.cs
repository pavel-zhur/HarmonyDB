using System.Globalization;
using OneShelf.Admin.Web.Models;
using OneShelf.Billing.Api.Client;
using OneShelf.Common.Database.Songs;
using OneShelf.Illustrations.Api.Client;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
});

builder.Configuration.AddJsonFile("appsettings.Secrets.json", true);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services
    .AddSongsDatabase()
    .AddIllustrationsApiClient(builder.Configuration)
    .AddBillingApiClient(builder.Configuration)
    .Configure<AdminOptions>(o => builder.Configuration.GetSection(nameof(AdminOptions)).Bind(o))
    .AddHttpClient();

var app = builder.Build();

app.UseRequestLocalization(o =>
{
    var defaultCulture = "en-US";
    o.SupportedCultures = new List<CultureInfo>
    {
        new(defaultCulture),
    };
    o.SetDefaultCulture(defaultCulture);
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
