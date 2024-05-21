using HarmonyDB.Common;
using OneShelf.Common;

namespace OneShelf.Frontend.Web.Models;

public class PrintPreferences
{
    public NoteAlteration? Alteration { get; set; }
    public Dictionary<int, int> Transpositions { get; set; } = new();
    public HashSet<int> TwoColumns { get; set; } = new();
    public HashSet<int> Exclusions { get; set; } = new();
    public Dictionary<int, string> Comments { get; set; } = new();
    public List<int>? Order { get; set; }
}