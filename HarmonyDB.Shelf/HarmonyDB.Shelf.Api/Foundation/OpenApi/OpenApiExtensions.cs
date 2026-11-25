using HarmonyDB.Common.ServiceAuth;
using HarmonyDB.Shelf.Model;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HarmonyDB.Shelf.Api.Foundation.OpenApi;

/// <summary>
/// The browser API's OpenAPI document, generated from the endpoints and their types: the endpoints under <c>/api</c>
/// only, without the internal ones (the internal API and the MCP are not the browser's). It is kept in
/// HarmonyDB.Shelf.Model/openapi.json and written there by the test OpenApiDocumentTests (see README.md).
/// </summary>
public static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddShelfOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new() { Title = "HarmonyDB Shelf", Version = DocumentName });
            options.SupportNonNullableReferenceTypes();
            options.NonNullableReferenceTypesAsRequired();
            options.OperationFilter<ErrorResponseOperationFilter>();
            options.DocInclusionPredicate((_, description) =>
                description.RelativePath?.StartsWith("api/", StringComparison.Ordinal) == true
                && !description.ActionDescriptor.EndpointMetadata.OfType<RequireInternalScopeAttribute>().Any());
        });

        // The schemas follow the JSON options of the endpoints (camel case, enums as strings).
        services.AddTransient<ISerializerDataContractResolver>(provider => new JsonSerializerDataContractResolver(
            provider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions));

        return services;
    }

    /// <summary>
    /// Every operation may answer an error, with <see cref="ErrorResponse"/>.
    /// </summary>
    private sealed class ErrorResponseOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Responses["default"] = new()
            {
                Description = "An error.",
                Content =
                {
                    ["application/json"] = new()
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(typeof(ErrorResponse), context.SchemaRepository),
                    },
                },
            };
        }
    }
}
