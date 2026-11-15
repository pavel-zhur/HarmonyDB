// Expected outputs worked out by hand from the C# (NodeHtml.CreateFinal and the Append* methods).

import { describe, expect, it } from 'vitest';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { NodeChild, NodeChord, NodeHtml, NodeText } from './index';
import { Note } from './Note';
import { RepresentationSettings } from './RepresentationSettings';
import { SimplificationMode } from './SimplificationMode';

const chord = (text: string) => new NodeChord({ children: [new NodeText(text).asChild()] }).asChild();
const text = (value: string) => new NodeText(value).asChild();
const pre = (...children: NodeChild[]) => new NodeHtml({ name: 'pre', children });

// a chords line over a lyrics line
const song = () => pre(chord('Am'), text('   '), chord('C'), text('\nHello world'));

describe('NodeHtml', () => {
    it('AsHtml: the chords line flies over the lyrics line', () => {
        expect(song().asHtml(new RepresentationSettings())).toBe(
            '<pre><div class=\'chords-double-line\'><span class=\'chords-line chords-double-line\'>'
            + '<span class=\'flying\'><span class=\'flying2\'><b class=\'chord\' chord-index=\'0\' chord-custom=\'\' chord-data=\'[!0!]m\'>Am</b></span></span>'
            + 'Hello'
            + '<span class=\'flying\'><span class=\'flying2\'><b class=\'chord\' chord-index=\'1\' chord-custom=\'\' chord-data=\'[!3!]\'>C</b></span></span>'
            + ' world</span></div></pre>');
    });

    it('AsHtml transposed, with custom attributes', () => {
        const html = song().asHtml(new RepresentationSettings({ transpose: 2, customAttributes: new Map([[1, 'x\'y']]) }));
        expect(html).toContain('chord-index=\'0\' chord-custom=\'\' chord-data=\'[!2!]m\'>Bm</b>');
        expect(html).toContain('chord-index=\'1\' chord-custom=\'x&#39;y\' chord-data=\'[!5!]\'>D</b>');
    });

    it('AsHtml: a simplified chord equal to the previous one becomes spaces', () => {
        const html = pre(chord('Am7'), text(' '), chord('Am')).asHtml(new RepresentationSettings({ simplification: SimplificationMode.Remove7 }));
        expect(html).toBe('<pre><span class=\'chords-line \'><b class=\'chord\' chord-index=\'0\' chord-custom=\'\' chord-data=\'[!0!]m\'>Am</b>    </span>\n</pre>');
    });

    it('AsHtml: the chord suffix is beautified', () => {
        expect(pre(chord('Cm7(b5)')).asHtml(new RepresentationSettings())).toBe(
            '<pre><span class=\'chords-line \'><b class=\'chord\' chord-index=\'0\' chord-custom=\'\' chord-data=\'[!3!]m7(b5)\'>Cm<sub>7</sub><sup>b5</sup></b></span>\n</pre>');
    });

    it('AsChords', () => {
        expect(song().asChords(new RepresentationSettings())).toEqual(['[!0!]m', '[!3!]']);
        expect(pre(chord('Bb/D'), chord('F#m')).asChords(new RepresentationSettings())).toEqual(['[!1b!]/[!5!]', '[!9#!]m']);
        expect(pre(chord('Bb/D')).asChords(new RepresentationSettings({ alteration: NoteAlteration.Sharp }))).toEqual(['[!1#!]/[!5#!]']);
    });

    it('AsChords simplified', () => {
        const chords = (value: string, simplification: SimplificationMode) => pre(chord(value)).asChords(new RepresentationSettings({ simplification }));
        expect(chords('Am7', SimplificationMode.Remove7)).toEqual(['[!0!]m']);
        expect(chords('Asus4', SimplificationMode.RemoveSus)).toEqual(['[!0!]5']);
        expect(chords('Cmaj9/E', SimplificationMode.All)).toEqual(['[!3!]maj']);
        expect(chords('C/E', SimplificationMode.RemoveBass)).toEqual(['[!3!]']);
        expect(chords('Am6', SimplificationMode.Remove6)).toEqual(['[!0!]m']);
    });

    it('AsLyrics', () => {
        expect(song().asLyrics()).toBe('Hello world');
    });

    it('AsOneShelf', () => {
        expect(song().asOneShelf(new RepresentationSettings())).toBe('>Am  C\nHello world\n');
    });

    it('a chord of dashes becomes text; a leading bar is split off', () => {
        expect(pre(chord('--'), text(' '), chord('|Am')).asChords(new RepresentationSettings())).toEqual(['[!0!]m']);
    });

    it('Fix0_Compress joins neighbouring texts', () => {
        const html = pre(text('a'), text('b'), chord('C'), text('c'));
        html.fix0_Compress();
        expect(html.children.map(x => x.nodeText?.text ?? 'chord')).toEqual(['ab', 'chord', 'c']);
        expect(html.getChordsCount()).toBe(1);
        expect(html.getNodesCount()).toBe(5);
    });
});

describe('Note', () => {
    it('Representation', () => {
        const settings = new RepresentationSettings();
        expect(new Note(1).representation(settings)).toBe('A#');
        expect(new Note(1).representation(new RepresentationSettings({ alteration: NoteAlteration.Flat }))).toBe('Bb');
        expect(new Note(1, NoteAlteration.Flat).representation(settings)).toBe('Bb');
        expect(new Note(1, NoteAlteration.Flat).representation(new RepresentationSettings({ transpose: 1 }))).toBe('B');
        expect(new Note(11).representation(new RepresentationSettings({ transpose: -13 }))).toBe('G'); // -13 folds to -1
        const relative = settings.with({ relativeTo: { note: 3, major: true } });
        expect(Note.E.representation(relative)).toBe('3');
        expect(new Note(1).representation(relative)).toBe('A#');
        expect(Note.E.representation(new RepresentationSettings({ testOnlyX: true }))).toBe('X');
    });

    it('CircleOfFifths', () => {
        const settings = new RepresentationSettings();
        const circle = Note.CircleOfFifths.map(x => `${x.major.representation(settings)}/${x.minor.representation(settings)}/${x.isSharps}`);
        expect(circle).toEqual([
            'C/A/true', 'G/E/true', 'D/B/true', 'A/F#/true', 'E/C#/true', 'B/G#/true', 'F#/D#/true',
            'Db/Bb/false', 'Ab/F/false', 'Eb/C/false', 'Bb/G/false', 'F/D/false',
        ]);
    });

    it('Sharp, Flat, Add, Normalize', () => {
        expect(Note.G.sharp().equals(new Note(11, NoteAlteration.Sharp))).toBe(true);
        expect(new Note(11).sharp().value).toBe(0);
        expect(Note.A.flat().equals(new Note(11, NoteAlteration.Flat))).toBe(true);
        expect(Note.C.add(-5).value).toBe(10);
        expect(Note.normalize(-13)).toBe(11);
    });

    it('RepresentationSettings: the constructor folds transposition, `with` does not', () => {
        expect(new RepresentationSettings({ transpose: 8 }).transpose).toBe(-4);
        expect(new RepresentationSettings({ transpose: 7 }).up().transpose).toBe(-4);
        expect(new RepresentationSettings().with({ transpose: 9 }).transpose).toBe(9);
        const settings = new RepresentationSettings();
        const a = settings.with({ relativeTo: { note: 1, major: true } });
        expect(a.equals(settings.with({ relativeTo: { note: 1, major: true } }))).toBe(true);
        // CustomAttributes is compared by reference, as in the C# record: two `new()` are not equal
        expect(new RepresentationSettings().equals(new RepresentationSettings())).toBe(false);
        expect(a.down().relativeTo).toBeNull();
    });
});
