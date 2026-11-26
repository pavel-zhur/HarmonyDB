using HarmonyDB.Analyses.Api;
using HarmonyDB.Index.Api;
using OneShelf.Authorization.Api;
using OneShelf.Billing.Api;
using OneShelf.Collectives.Api;
using OneShelf.Common.Api.Hosting;
using OneShelf.Frontend.Api;
using OneShelf.Illustrations.Api;
using OneShelf.OneDog.Runner.Functions;
using OneShelf.OneDragon.Runner.Functions;
using OneShelf.Sources.Self.Api;
using OneShelf.Telegram.Runner.Functions;

// The services of this repository in one process, each a module under its own path base (see ApiModule).
var builder = WebApplication.CreateBuilder(args);

builder.AddApiModules(
    new IndexModule(),
    new AnalysesModule(),
    new AuthorizationModule(),
    new BillingModule(),
    new CollectivesModule(),
    new FrontendModule(),
    new IllustrationsModule(),
    new SourcesSelfModule(),
    new TelegramModule(),
    new OneDogModule(),
    new OneDragonModule());

var app = builder.Build();
app.MapApiModules();
app.Run();
