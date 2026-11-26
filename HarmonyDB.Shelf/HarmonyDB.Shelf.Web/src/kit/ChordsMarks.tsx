// What a source says of an item of chords, beside the source wherever it is shown (a search's row, a song's variant,
// the chords' head), as the old shelf showed it there: the source's pick (in its words when it has them), its rating,
// and the item's type at the source, quieter when the source does not fully support it.

import { useTranslation } from 'react-i18next';
import type { Schemas } from '../api/client';

type Header = Schemas['ChordsHeader'];

export function ChordsMarks({ header }: { header: Header }) {
    const { t } = useTranslation();
    const { isChosen, chosenText, isSupported } = header.marks;
    const rating = header.rating ?? null;
    if (!isChosen && rating === null && !header.type) return null;
    return (
        <span className="cmk">
            {isChosen && <span className="ch">{chosenText || t('song.source.chosen')}</span>}
            {/* rounded as the old shelf rounded it */}
            {rating !== null && <span className="rt">☆ {Math.round(rating * 100) / 100}</span>}
            {header.type && (
                <span className={isSupported === false ? 'ty off' : 'ty'} title={isSupported === false ? t('song.source.notSupported') : undefined}>
                    {header.type}
                </span>
            )}
        </span>
    );
}
