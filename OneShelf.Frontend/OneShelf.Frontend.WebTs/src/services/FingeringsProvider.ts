// The original: Services/FingeringsProvider.cs.
//
// The model is the original's embedded resource Resources/fingerings.json (src/resources/fingerings.json), read
// the first time GetModel is called. The original has it inside its assembly, downloaded with the application; here
// it is a separate file of the build, fetched at that first call.
//
// SynonymsTextToHtmls is an ILookup<string, string>: synonymsTextToHtmls.get(text) gives the HTMLs (none for an
// unknown text).

import { Lookup, toDictionary } from '../dotnet/collections';
import { defaultJsonSerializerOptions, deserialize } from '../lib/common/dotnet/json/JsonSerializer';
import { FingeringsModel } from '../models/Fingerings/FingeringsModel';
import { FingeringsModelIndex } from '../models/Fingerings/FingeringsModelIndex';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';

export class FingeringsProvider {
    private _cache: FingeringsModelIndex | null = null;
    private readonly _lock = new AsyncLock();

    readonly synonymsTextToHtmls: Lookup<string, string>;

    readonly synonymsTextToText: ReadonlyMap<string, string>;

    constructor() {
        const synonyms: { text: string; synonymText: string; synonymHtml: string }[] = [
            { text: 'C+', synonymText: 'C5+', synonymHtml: 'C<sup>5+</sup>' },
            { text: 'C+', synonymText: 'Caug', synonymHtml: 'Caug' },
            { text: 'C75+', synonymText: 'Caug7', synonymHtml: 'Caug<sub>7</sub>' },
            { text: 'Cmaj5-', synonymText: 'Cmaj75-', synonymHtml: 'Cmaj<sub>7</sub><sup>5-</sup>' },
            { text: 'Cmaj', synonymText: 'Cmaj7', synonymHtml: 'Cmaj<sub>7</sub>' },
            { text: 'Csus4', synonymText: 'Csus', synonymHtml: 'Csus' },
            { text: 'C6sus4', synonymText: 'C6sus', synonymHtml: 'C<sub>6</sub>sus' },
            { text: 'C7sus4', synonymText: 'C7sus', synonymHtml: 'C<sub>7</sub>sus' },
            { text: 'Cm7+', synonymText: 'Cmmaj7', synonymHtml: 'Cmmaj<sub>7</sub>' },
            { text: 'Cm5-', synonymText: 'Cdim', synonymHtml: 'Cdim' },
            { text: 'Cadd9', synonymText: 'Cadd2', synonymHtml: 'Cadd<sub>2</sub>' },
            { text: 'Cadd11', synonymText: 'Cadd4', synonymHtml: 'Cadd<sub>4</sub>' },
            { text: 'Cmadd9', synonymText: 'Cmadd2', synonymHtml: 'Cmadd<sub>2</sub>' },
            { text: 'Cmadd11', synonymText: 'Cmadd4', synonymHtml: 'Cmadd<sub>4</sub>' },
        ];

        this.synonymsTextToHtmls = Lookup.from(synonyms, x => x.text, x => x.synonymHtml);
        this.synonymsTextToText = toDictionary(synonyms, x => x.synonymText, x => x.text);
    }

    async getModel(): Promise<FingeringsModelIndex> {
        if (this._cache !== null) return this._cache;

        const _ = await this._lock.lockAsync();
        try {
            if (this._cache !== null) return this._cache;
            const resource = (await import('../resources/fingerings.json?raw')).default;

            const model = deserialize(resource, FingeringsModel.json, defaultJsonSerializerOptions);
            this._cache = new FingeringsModelIndex({
                model: model!,
                typesByChordText: toDictionary(model.types, x => x.chordText, x => x),
            });

            return this._cache;
        } finally {
            _.dispose();
        }
    }

    replaceTitle(title: string): string {
        return title.replaceAll('w', '#').replaceAll('H', 'B');
    }
}
