using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Playground.Web.Tools;

public static class OutputExtensions
{
    public static string ToTitle(this IndexHeaderBase header) => $"{string.Join(", ", header.Artists ?? [])} – {header.Title}";
}