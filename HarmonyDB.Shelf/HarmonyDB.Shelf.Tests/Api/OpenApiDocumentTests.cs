using System.Runtime.CompilerServices;
using HarmonyDB.Shelf.Api.Foundation.OpenApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace HarmonyDB.Shelf.Tests.Api;

/// <summary>
/// The committed HarmonyDB.Shelf.Model/openapi.json is the document the code makes. To write it anew, run the tests
/// with SHELF_WRITE_OPENAPI=1.
/// </summary>
public sealed class OpenApiDocumentTests
{
    [Fact]
    public void CommittedDocumentIsCurrent()
    {
        // The plain API, without the test area of ShelfApiFactory.
        using var factory = new PlainApiFactory();
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger(OpenApiExtensions.DocumentName);
        var current = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0).ReplaceLineEndings("\n") + "\n";

        var path = CommittedPath();
        if (Environment.GetEnvironmentVariable("SHELF_WRITE_OPENAPI") == "1")
        {
            File.WriteAllText(path, current);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing: run the tests with SHELF_WRITE_OPENAPI=1.");
        Assert.True(
            File.ReadAllText(path).ReplaceLineEndings("\n") == current,
            $"{path} is not what the code makes: run the tests with SHELF_WRITE_OPENAPI=1 and commit it.");
    }

    private static string CommittedPath([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "HarmonyDB.Shelf.Model", "openapi.json"));

    private sealed class PlainApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Shelf", "Host=localhost;Database=shelf-tests");
            builder.UseSetting("Shelf:Secrets:Key", Convert.ToBase64String(new byte[32]));
        }
    }
}
