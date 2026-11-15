import type { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { Note } from './Note';
import { SimplificationMode } from './SimplificationMode';

export interface RepresentationSettingsConstructorArguments {
    transpose?: number;
    alteration?: NoteAlteration | null;
    testOnlyX?: boolean;
    isVariableWidth?: boolean;
    simplification?: SimplificationMode;
    customAttributes?: ReadonlyMap<number, string> | null;
}

export interface RepresentationSettingsMembers {
    readonly transpose: number;
    readonly alteration: NoteAlteration | null;
    readonly testOnlyX: boolean;
    readonly relativeTo: { readonly note: number; readonly major: boolean } | null;
    readonly isVariableWidth: boolean;
    readonly simplification: SimplificationMode;
    readonly customAttributes: ReadonlyMap<number, string>;
}

/** A record. `with` copies the members and sets the given ones as they are (the constructor's normalization does not apply). */
export class RepresentationSettings implements RepresentationSettingsMembers {
    readonly transpose: number;
    readonly alteration: NoteAlteration | null;
    readonly testOnlyX: boolean;
    readonly relativeTo: { readonly note: number; readonly major: boolean } | null;
    readonly isVariableWidth: boolean;
    readonly simplification: SimplificationMode;
    readonly customAttributes: ReadonlyMap<number, string>;

    /** new RepresentationSettings(transpose: ..., alteration: ..., ...) — the C# optional parameters, by name. */
    constructor(args: RepresentationSettingsConstructorArguments = {}) {
        const transpose = args.transpose ?? 0;
        this.transpose = transpose > 7 ? transpose - Note.Modulus : transpose < -7 ? transpose + Note.Modulus : transpose;
        this.alteration = args.alteration ?? null;
        this.testOnlyX = args.testOnlyX ?? false;
        this.relativeTo = null;
        this.isVariableWidth = args.isVariableWidth ?? false;
        this.simplification = args.simplification ?? SimplificationMode.None;
        this.customAttributes = args.customAttributes ?? new Map<number, string>();
    }

    /** `settings with { ... }` */
    with(changes: Partial<RepresentationSettingsMembers>): RepresentationSettings {
        return Object.assign(Object.create(RepresentationSettings.prototype) as RepresentationSettings, this, changes);
    }

    down(): RepresentationSettings {
        return new RepresentationSettings({
            transpose: this.transpose - 1,
            alteration: this.alteration,
            testOnlyX: this.testOnlyX,
            isVariableWidth: this.isVariableWidth,
            simplification: this.simplification,
            customAttributes: this.customAttributes,
        });
    }

    up(): RepresentationSettings {
        return new RepresentationSettings({
            transpose: this.transpose + 1,
            alteration: this.alteration,
            testOnlyX: this.testOnlyX,
            isVariableWidth: this.isVariableWidth,
            simplification: this.simplification,
            customAttributes: this.customAttributes,
        });
    }

    /** Record equality: RelativeTo by value, CustomAttributes by reference. */
    equals(other: RepresentationSettings | null | undefined): boolean {
        return other != null
            && this.transpose === other.transpose
            && this.alteration === other.alteration
            && this.testOnlyX === other.testOnlyX
            && (this.relativeTo === other.relativeTo
                || (this.relativeTo != null && other.relativeTo != null
                    && this.relativeTo.note === other.relativeTo.note && this.relativeTo.major === other.relativeTo.major))
            && this.isVariableWidth === other.isVariableWidth
            && this.simplification === other.simplification
            && this.customAttributes === other.customAttributes;
    }
}
