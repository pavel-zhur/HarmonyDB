// The original: Shared/CircleOfFifths.razor.
//
// The Chords parameter's setter computes the set of (note, chord type) pairs each time the parameter is set, that is
// each time the owner renders it with a list (a new list each time): here, each time the list given is another one.

import { useMemo } from 'react';
import { stringAttributes } from '../framework/attributes';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { ChordType } from '../lib/analysis/Models/ChordType';
import { distinct, groupBy } from '../lib/common/dotnet/Linq';
import { Note, RepresentationSettings } from '../lib/representations';
import { services } from '../services/registry';
import './CircleOfFifths.scoped.less';

export interface CircleOfFifthsProps {
    played: (note: Note, chordType: ChordType) => void;
    playOnClick: boolean;
    chords?: string[] | null;
}

function key(note: number, chordType: ChordType): string {
    return `${note}|${chordType}`;
}

export function CircleOfFifths({ played, playOnClick, chords = null }: CircleOfFifthsProps) {
    const chordDataParser = services.chordDataParser;
    const player = services.player;

    // HashSet<(byte note, ChordType type)>?
    const chordsSet = useMemo(() => {
        if (chords === null) return null;

        const pairs = distinct(distinct(chords)
            .map(x => chordDataParser.getChord(x))
            .filter(x => x !== null)
            .flatMap(x => {
                const y = [{ note: x.root, chordTypeForCircleOfFifths: x.chordTypeForCircleOfFifths }];
                return x.bass === null ? y : [...y, { note: x.bass, chordTypeForCircleOfFifths: ChordType.Unknown }];
            }));

        return new Set(groupBy(pairs, x => x.note)
            .flatMap(g => g.items.some(x => x.chordTypeForCircleOfFifths === ChordType.Major || x.chordTypeForCircleOfFifths === ChordType.Minor)
                ? g.items.filter(x => x.chordTypeForCircleOfFifths !== ChordType.Unknown)
                : g.items)
            .map(x => key(x.note, x.chordTypeForCircleOfFifths)));
    }, [chords, chordDataParser]);

    const getStyle = (note: number, chordType: ChordType): string => {
        if (chordsSet === null) return '';

        if (chordsSet.has(key(note, chordType))) return 'background: lightblue; box-shadow: 0px 0px 5px 5px lightblue';
        if (chordsSet.has(key(note, ChordType.Unknown))) return 'background: lightyellow; box-shadow: 0px 0px 5px 5px lightyellow';

        return '';
    };

    const play = async (note: Note, chordType: ChordType) => {
        if (playOnClick) {
            await player.playChord(`[!${note.value}!]${chordType === ChordType.Major ? '' : 'm'}`);
        }

        played(note, chordType);
    };

    return (
        <div className="circle-of-fifths mb-4 mt-2">
            {Note.CircleOfFifths.map(({ major: currentMajor, minor: currentMinor }, i) => (
                <div key={i} className="key">
                    <div className="major" onClick={() => void errorHandledTask(play(currentMajor, ChordType.Major))}>
                        <div ref={stringAttributes({ style: getStyle(currentMajor.value, ChordType.Major) })}>{currentMajor.representation(new RepresentationSettings())}</div>
                    </div>
                    {'\n            '}
                    <div className="minor" onClick={() => void errorHandledTask(play(currentMinor, ChordType.Minor))}>
                        <div ref={stringAttributes({ style: getStyle(currentMinor.value, ChordType.Minor) })}>{currentMinor.representation(new RepresentationSettings())}m</div>
                    </div>
                </div>
            ))}
        </div>
    );
}
