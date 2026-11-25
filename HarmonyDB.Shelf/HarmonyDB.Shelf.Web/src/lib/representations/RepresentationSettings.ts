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

/** The settings set with an initializer: `new RepresentationSettings(...) { ShowDegrees = ..., ... }`. */
export interface RepresentationSettingsInit {
    showDegrees?: boolean;
    chordSuperscripts?: boolean;
    showNotesAsWritten?: boolean;
}

export interface RepresentationSettingsMembers {
    readonly transpose: number;
    readonly alteration: NoteAlteration | null;
    readonly testOnlyX: boolean;
    readonly relativeTo: { readonly note: number; readonly major: boolean } | null;
    readonly isVariableWidth: boolean;
    readonly simplification: SimplificationMode;
    readonly customAttributes: ReadonlyMap<number, string>;
    readonly showDegrees: boolean;
    readonly chordSuperscripts: boolean;
    readonly showNotesAsWritten: boolean;
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

    /** Show the chords' degrees (in the key where the chord stands), where the sheet has them. */
    readonly showDegrees: boolean;

    /**
     * Draw the chord names with their degrees lowered and their altered degrees raised (subscripts and superscripts):
     * in Cm7b5 the 7 is a subscript and the b5 a superscript. Off, the chord names are plain text.
     */
    readonly chordSuperscripts: boolean;

    /**
     * Not transposed, and with no sharps or flats asked for (`alteration`), show the roots and the basses written with one sign
     * as they are written: E#m, Cb, Am/Fb. Off, these are named by their pitch: Fm, B, Am/E.
     * A note written with two signs (F##m, Bbb7) is shown as written either way.
     */
    readonly showNotesAsWritten: boolean;

    /** new RepresentationSettings(transpose: ..., alteration: ..., ...) — the C# optional parameters, by name. */
    constructor(args: RepresentationSettingsConstructorArguments = {}, init: RepresentationSettingsInit = {}) {
        const transpose = args.transpose ?? 0;
        this.transpose = transpose > 7 ? transpose - Note.Modulus : transpose < -7 ? transpose + Note.Modulus : transpose;
        this.alteration = args.alteration ?? null;
        this.testOnlyX = args.testOnlyX ?? false;
        this.relativeTo = null;
        this.isVariableWidth = args.isVariableWidth ?? false;
        this.simplification = args.simplification ?? SimplificationMode.None;
        this.customAttributes = args.customAttributes ?? new Map<number, string>();
        this.showDegrees = init.showDegrees ?? true;
        this.chordSuperscripts = init.chordSuperscripts ?? true;
        this.showNotesAsWritten = init.showNotesAsWritten ?? false;
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
        }, this.init());
    }

    up(): RepresentationSettings {
        return new RepresentationSettings({
            transpose: this.transpose + 1,
            alteration: this.alteration,
            testOnlyX: this.testOnlyX,
            isVariableWidth: this.isVariableWidth,
            simplification: this.simplification,
            customAttributes: this.customAttributes,
        }, this.init());
    }

    private init(): RepresentationSettingsInit {
        return { showDegrees: this.showDegrees, chordSuperscripts: this.chordSuperscripts, showNotesAsWritten: this.showNotesAsWritten };
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
            && this.customAttributes === other.customAttributes
            && this.showDegrees === other.showDegrees
            && this.chordSuperscripts === other.chordSuperscripts
            && this.showNotesAsWritten === other.showNotesAsWritten;
    }
}
