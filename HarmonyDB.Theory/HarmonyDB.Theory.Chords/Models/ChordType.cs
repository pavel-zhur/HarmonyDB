using HarmonyDB.Theory.Chords.Models.Enums;
using OneShelf.Common;

namespace HarmonyDB.Theory.Chords.Models;

public record ChordType(ChordMainType Type, ChordTypeExtension? Extension, ChordTypeAdditions Additions)
{
    public string ToCanonical()
    {
        var tokens = new ChordTypeToken(Type).Once().ToList();
        if (Extension.HasValue)
        {
            tokens.Add(new(Extension.Value));
        }

        tokens.AddRange(Enum
            .GetValues<ChordTypeAdditions>()
            .Where(x => x > ChordTypeAdditions.None)
            .Where(x => Additions.HasFlag(x))
            .Select(x => new ChordTypeToken(x)));

        return string.Join(string.Empty, tokens.Select(x => x.ToCanonical()));
    }
}