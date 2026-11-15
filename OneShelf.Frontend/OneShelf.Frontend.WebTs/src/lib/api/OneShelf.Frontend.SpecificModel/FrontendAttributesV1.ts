import type { SourceColor } from './SourceColor';

export class FrontendAttributesV1 {
    readonly badgeText: string | null;
    readonly sourceColor: SourceColor;

    constructor(init: { badgeText: string | null; sourceColor: SourceColor }) {
        this.badgeText = init.badgeText;
        this.sourceColor = init.sourceColor;
    }
}
