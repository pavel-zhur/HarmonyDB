using HarmonyDB.Shelf.Api.Areas.App;
using HarmonyDB.Shelf.Api.Areas.Auth;
using HarmonyDB.Shelf.Api.Areas.Me;
using HarmonyDB.Shelf.Api.Foundation;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Database;

var builder = WebApplication.CreateBuilder(args);
builder.AddShelfFoundation();

// The areas of the API: one line each (see README.md, "An area").
builder.AddArea<AppArea>();
builder.AddArea<AuthArea>();
builder.AddArea<MeArea>();

var app = builder.Build();
app.UseShelfFoundation();
app.MapAreas();

await app.MigrateShelfDatabaseIfConfiguredAsync();
await app.RunAsync();

/// <summary>
/// Visible to the tests' host.
/// </summary>
public partial class Program;
