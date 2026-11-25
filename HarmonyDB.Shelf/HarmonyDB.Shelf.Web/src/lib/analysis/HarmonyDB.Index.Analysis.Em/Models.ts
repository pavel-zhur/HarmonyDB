// From HarmonyDB.Index.Analysis.Em (namespace HarmonyDB.Index.Analysis.Em.Models: Scale.cs, Constants.cs, ISource.cs,
// Loop.cs, Song.cs): only the types the analysis library uses. The site does not use the Em project, so the rest of it
// (Constants.Indices/Pairs/GetMajorTonic/GetRelativeScale, EmContext, LoopLink, IEmModel, MusicAnalyzer) is not ported.
// A `float[,]` ([TonicCount, ScaleCount]) is an array of TonicCount rows of ScaleCount floats; Float32Array keeps the
// values in single precision.

export enum Scale {
    Major,
    Minor,
}

export const Constants = {
    TonicCount: 12, // Number of different tonics
    ScaleCount: 2, // Major and Minor
} as const;

function newProbabilities(): Float32Array[] {
    return Array.from({ length: Constants.TonicCount }, () => new Float32Array(Constants.ScaleCount));
}

/** (float TonicScore, float ScaleScore) */
export interface Score {
    readonly tonicScore: number;
    readonly scaleScore: number;
}

/** (byte Tonic, Scale Scale) */
export interface KnownTonality {
    readonly tonic: number;
    readonly scale: Scale;
}

export interface ISource {
    readonly id: string;
    tonalityProbabilities: Float32Array[]; // [TonicCount, ScaleCount]
    score: Score;
}

export class Loop implements ISource {
    constructor(init: { id: string }) {
        this.id = init.id;
    }

    readonly id: string;
    tonalityProbabilities: Float32Array[] = newProbabilities();
    score: Score = { tonicScore: 0, scaleScore: 0 };
}

export class Song implements ISource {
    constructor(init: { id: string; knownTonality: KnownTonality | null }) {
        this.id = init.id;
        this.knownTonality = init.knownTonality;
    }

    readonly id: string;
    tonalityProbabilities: Float32Array[] = newProbabilities();
    score: Score = { tonicScore: 0, scaleScore: 0 };
    readonly knownTonality: KnownTonality | null;
}
