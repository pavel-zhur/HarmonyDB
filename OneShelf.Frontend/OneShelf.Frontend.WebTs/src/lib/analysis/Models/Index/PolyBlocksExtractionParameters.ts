import type { IValueEquatable } from '../../../common/dotnet/Equality';

/** A record: equal by its members. */
export class PolyBlocksExtractionParameters implements IValueEquatable {
    constructor(init: Partial<PolyBlocksExtractionParameters> = {}) {
        Object.assign(this, init);
    }

    sequencesExcludeIfOverLoops = true;
    sequencesExcludeIfOverLoopsWithTails = true;
    sequencesExcludeIfExtendsLeftWithoutLosingOccurrences = true;
    sequencesExcludeIfExtendsRightWithoutLosingOccurrencesOrOverlappingWithItself = true;

    sequencesExcludeIfContainsAllChordsMoreThanOnce = false;
    sequencesExcludeIfContainsSuccessiveLoopsBreak = false;

    loopsExcludeIfContainsAllChordsMoreThanOnce = false;
    loopsExcludeIfContainsSuccessiveLoopsBreak = false;

    equalityKey(): string {
        return [
            this.sequencesExcludeIfOverLoops,
            this.sequencesExcludeIfOverLoopsWithTails,
            this.sequencesExcludeIfExtendsLeftWithoutLosingOccurrences,
            this.sequencesExcludeIfExtendsRightWithoutLosingOccurrencesOrOverlappingWithItself,
            this.sequencesExcludeIfContainsAllChordsMoreThanOnce,
            this.sequencesExcludeIfContainsSuccessiveLoopsBreak,
            this.loopsExcludeIfContainsAllChordsMoreThanOnce,
            this.loopsExcludeIfContainsSuccessiveLoopsBreak,
        ].map(x => (x ? 1 : 0)).join('');
    }
}
