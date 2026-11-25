using HarmonyDB.Shelf.Api.Areas.Analyses;
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
builder.AddArea<AnalysesArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Jobs.JobsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Admin.AdminArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Playlists.PlaylistsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Library.LibraryArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Share.ShareArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Sync.SyncArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Chords.ChordsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Search.SearchArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.AnalysisTools.AnalysisToolsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Music.MusicArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Recordings.RecordingsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Media.MediaArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.OwnChords.OwnChordsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Illustrations.IllustrationsArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Book.BookArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Import.ImportArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.OAuth.OAuthArea>();
builder.AddArea<HarmonyDB.Shelf.Api.Areas.Mcp.McpArea>();

var app = builder.Build();
app.UseShelfFoundation();
app.MapAreas();

await app.MigrateShelfDatabaseIfConfiguredAsync();
await app.RunAsync();

/// <summary>
/// Visible to the tests' host.
/// </summary>
public partial class Program;
