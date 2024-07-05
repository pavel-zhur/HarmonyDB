using HarmonyDB.Index.BusinessLogic;
using HarmonyDB.Index.BusinessLogic.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Secrets.json", true);

var services = builder.Services;

services
    .AddIndexBusinessLogic(builder.Configuration);

var host = builder.Build();

var tonalitiesIndexCache = host.Services.GetRequiredService<TonalitiesIndexCache>();

await tonalitiesIndexCache.Rebuild(20000);