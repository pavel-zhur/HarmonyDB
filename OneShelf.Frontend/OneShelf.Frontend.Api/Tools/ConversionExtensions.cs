using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Api.Tools
{
    public static class ConversionExtensions
    {
        public static string ToPdfConfiguration(this GetPdfsChunkRequestFile x)
        {
            return $"{x.Artists} - {x.Title}, {x.TwoColumns}, {x.Source}, {x.Alteration}, {x.Transpose}";
        }
    }
}
