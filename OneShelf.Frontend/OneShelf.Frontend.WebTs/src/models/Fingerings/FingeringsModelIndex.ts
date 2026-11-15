// The original: Models/Fingerings/FingeringsModelIndex.cs.

import type { FingeringsChordType } from './FingeringsChordType';
import type { FingeringsModel } from './FingeringsModel';

export class FingeringsModelIndex {
    readonly model: FingeringsModel;
    readonly typesByChordText: ReadonlyMap<string, FingeringsChordType>;

    constructor(init: { model: FingeringsModel; typesByChordText: ReadonlyMap<string, FingeringsChordType> }) {
        this.model = init.model;
        this.typesByChordText = init.typesByChordText;
    }
}