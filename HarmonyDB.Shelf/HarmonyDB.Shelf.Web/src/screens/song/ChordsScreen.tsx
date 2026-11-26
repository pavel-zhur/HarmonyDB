// Chords alone, by their id (a link to chords, a search result of a guest): /chords/:chordsId; ?address= — the item's
// own link the person came by (the public page leads here with it), passed on with its readings (data/address.ts).

import { useParams, useSearchParams } from 'react-router';
import { cameBy } from '../../data/address';
import { SongPane } from './SongPane';

export function ChordsScreen() {
    const { chordsId } = useParams() as { chordsId: string };
    const [query] = useSearchParams();
    cameBy(chordsId, query.get('address'));
    return (
        <div className="pane song">
            <SongPane target={{ chords: chordsId }} closeTo="/" />
        </div>
    );
}
