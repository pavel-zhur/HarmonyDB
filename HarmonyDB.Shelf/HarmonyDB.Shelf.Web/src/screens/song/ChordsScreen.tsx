// Chords alone, by their address (a link to chords, a search result of a guest): /chords/:chordsId.

import { useParams } from 'react-router';
import { SongPane } from './SongPane';

export function ChordsScreen() {
    const { chordsId } = useParams() as { chordsId: string };
    return (
        <div className="pane song">
            <SongPane target={{ chords: chordsId }} closeTo="/" />
        </div>
    );
}
