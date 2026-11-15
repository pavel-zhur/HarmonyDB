using OneShelf.Common.Api.Hosting;
using OneShelf.Pdfs.Api;

// The PDF service, a module under its own path base (see ApiModule). It runs alone: it carries a Chrome, and its
// conversions take much memory at times.
var builder = WebApplication.CreateBuilder(args);

builder.AddApiModules(new PdfsModule());

var app = builder.Build();
app.MapApiModules();
app.Run();
