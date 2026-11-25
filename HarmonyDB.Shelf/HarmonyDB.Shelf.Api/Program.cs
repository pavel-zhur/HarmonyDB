using HarmonyDB.Shelf.Api.Areas.App;
using HarmonyDB.Shelf.Api.Foundation;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Database;

var builder = WebApplication.CreateBuilder(args);
builder.AddShelfFoundation();

// The areas of the API: one line each (see README.md, "An area").
builder.AddArea<AppArea>();

var app = builder.Build();
app.UseShelfFoundation();
app.MapAreas();

await app.MigrateShelfDatabaseIfConfiguredAsync();
await app.RunAsync();

/// <summary>
/// Visible to the tests' host.
/// </summary>
public partial class Program;
