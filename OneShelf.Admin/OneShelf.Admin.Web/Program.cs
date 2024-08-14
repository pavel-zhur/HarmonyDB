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
    .AddBillingApiClient(builder.Configuration);

var app = builder.Build();

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
