using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.BusinessLogic.Caches;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Secrets.json", true);

var services = builder.Services;

services
    .AddIndexBusinessLogic(builder.Configuration);

var host = builder.Build();

var tonalitiesIndexCache = host.Services.GetRequiredService<TonalitiesCache>();

await tonalitiesIndexCache.Rebuild(20000);