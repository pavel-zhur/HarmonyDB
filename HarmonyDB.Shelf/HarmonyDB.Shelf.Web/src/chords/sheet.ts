// The chord sheet as the API sends it (camel case, enums as words) made into the sheet's classes (lib/representations),
// which render it exactly as the .NET side does.

import type { Schemas } from '../api/client';
import { NoteAlteration } from '../lib/common/HarmonyDB.Common/NoteAlteration';
import { NodeChild, NodeChord, NodeHtml, NodeLine, NodeMark, NodeNote, NodeText, Note, SheetBar, SheetKey, SheetPlace, SheetTempo, SheetTimes, SheetTimeSignature } from '../lib/representations';

type ApiNodeHtml = Schemas['NodeHtml'];
type ApiNodeChild = Schemas['NodeChild'];

const key = (value: Schemas['SheetKey']) => new SheetKey(value.tonic, value.isMinor);
const place = (value: Schemas['SheetPlace'] | undefined) => (value ? new SheetPlace(value.bar, value.beat, value.at ?? null) : null);
const times = (value: Schemas['SheetTimes'] | undefined) => (value
    ? new SheetTimes({ end: value.end, bars: value.bars.map(x => new SheetBar({ beats: x.beats, timeSignature: x.time_sig ?? null })) })
    : null);

function child(value: ApiNodeChild): NodeChild {
    if (value.nodeChord) {
        return new NodeChord({
            children: value.nodeChord.children.map(child),
            degree: value.nodeChord.degree ?? null,
            bassDegree: value.nodeChord.bassDegree ?? null,
            time: value.nodeChord.time ?? null,
            place: place(value.nodeChord.place),
        }).asChild();
    }

    if (value.nodeText) return new NodeText(value.nodeText.text, value.nodeText.time ?? null).asChild();

    if (value.nodeNote) {
        const alteration = value.nodeNote.note.alteration === 'flat' ? NoteAlteration.Flat : value.nodeNote.note.alteration === 'sharp' ? NoteAlteration.Sharp : null;
        const node = new NodeNote(new Note(value.nodeNote.note.value, alteration));
        node.accidentals = value.nodeNote.accidentals ?? null;
        return node.asChild();
    }

    if (value.nodeHtml) return sheetFromApi(value.nodeHtml).asChild();
    if (value.nodeLine) return new NodeLine({ children: value.nodeLine.children.map(child) }).asChild();

    if (value.nodeMark) {
        const mark = value.nodeMark;
        return new NodeMark({
            key: mark.key ? key(mark.key) : null,
            tempo: mark.tempo ? new SheetTempo(mark.tempo.beatsPerMinute, mark.tempo.unit, mark.tempo.dotted) : null,
            timeSignature: mark.timeSignature ? new SheetTimeSignature(mark.timeSignature.beats, mark.timeSignature.unit) : null,
            time: mark.time ?? null,
            place: place(mark.place),
            // left out by the server when false
            meter: mark.meter ?? false,
        }).asChild();
    }

    throw new Error('A node of the sheet is empty.');
}

export function sheetFromApi(value: ApiNodeHtml): NodeHtml {
    return new NodeHtml({
        name: value.name,
        classes: value.classes ?? null,
        keys: value.keys?.map(key) ?? null,
        times: times(value.times),
        children: value.children.map(child),
    });
}
