using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Sources.Self.Api.Model;
using OneShelf.Sources.Self.Api.Model.V1;
using OneShelf.Sources.Self.Api.Services;

namespace OneShelf.Sources.Self.Api.Functions.V1.Own
{
    public class FormatPreview : FunctionBase<FormatPreviewRequest, FormatPreviewResponse>
    {
        private readonly StructureParser _structureParser;

        public FormatPreview(ILoggerFactory loggerFactory, StructureParser structureParser)
            : base(loggerFactory)
        {
            _structureParser = structureParser;
        }

        [Function(SelfApiUrls.V1FormatPreview)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] FormatPreviewRequest request) => RunHandler(request);

        protected override async Task<FormatPreviewResponse> Execute(FormatPreviewRequest request) =>
            new()
            {
                Output = _structureParser.ContentToHtml(request.Content),
            };
    }
}
