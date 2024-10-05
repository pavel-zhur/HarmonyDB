using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public record ChordType(ChordMainType Type, ChordTypeExtension? Extension, ChordTypeAdditions Additions, byte? Fret);