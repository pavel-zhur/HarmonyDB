// The original: Shared/FingeringIndicator.razor.

import { useRef } from 'react';
import { getRequired } from '../dotnet/collections';
import { delay } from '../dotnet/tasks';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { inlineEventAttribute } from '../framework/inlineEventAttribute';
import { asyncVoid } from '../framework/unhandledExceptions';
import { useEvent } from '../hooks/useEvent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { toBase64String } from '../lib/common/dotnet/Convert';
import type { FingeringsChordType } from '../models/Fingerings/FingeringsChordType';
import { FingeringsNote } from '../models/Fingerings/FingeringsNote';
import type { Player, PlayedChord } from '../services/Player';
import { services } from '../services/registry';
import './FingeringIndicator.scoped.scss';

export function FingeringIndicator() {
    const player = services.player;
    const fingeringsProvider = services.fingeringsProvider;
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({
        title: null as string | null,
        e: null as PlayedChord | null,
        pictures: null as readonly string[] | null,
    }).current;

    // async void PlayerOnChordPlayed (it awaits nothing)
    const playerOnChordPlayed = (_sender: Player, e: PlayedChord | null): void => asyncVoid(async () => {
        if (e === null) {
            fields.title = null;
            fields.e = null;
            fields.pictures = null;
            stateHasChanged();
            return;
        }

        fields.e = e;
        fields.pictures = null;
        const fingeringsNote = e.note as FingeringsNote;
        fields.title = `${fingeringsProvider.replaceTitle(FingeringsNote[fingeringsNote] ?? String(fingeringsNote))}${e.fingering}`;
        stateHasChanged();
    });

    // OnInitializedAsync: Player.ChordPlayed += PlayerOnChordPlayed;
    useEvent(player.chordPlayed, playerOnChordPlayed);

    const load = async (): Promise<void> => {
        if (fields.e === null) return;

        await delay(100);
        const model = await fingeringsProvider.getModel();
        if (fields.e === null) {
            fields.pictures = [];
            return;
        }

        let type: FingeringsChordType;
        const byText = model.typesByChordText.get('C' + fields.e.fingering);
        if (byText !== undefined) {
            type = byText;
        } else {
            const text = fingeringsProvider.synonymsTextToText.get('C' + fields.e.fingering);
            if (text !== undefined) {
                type = getRequired(model.typesByChordText, text);
            } else {
                fields.pictures = [];
                return;
            }
        }

        fields.pictures = getRequired(type.notes, fields.e.note as FingeringsNote)
            .map(x => getRequired(model.model.pictures, x))
            .map(toBase64String);
        stateHasChanged();
    };

    if (fields.title === null) return null;

    return (
        <li className="nav-item nav-item-dropdown-lg dropdown ms-lg-2 fingering-shown">
            <span className="my-link navbar-nav-link ps-1 pe-2" data-bs-toggle="dropdown" data-bs-auto-close="outside" onClick={blazorEventHandler(stateHasChanged, load)}>
                <small id="fingering-title">{fields.title}</small>
            </span>

            <div className="dropdown-menu dropdown-menu-end wmin-lg-400 p-0" data-bs-popper="static">
                <div className="d-flex align-items-center p-3">
                    <h6 className="mb-0">Аппликатуры</h6>
                </div>
                <div className="p-3 pt-0">
                    {fields.pictures === null
                        ? <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
                        : fields.pictures.length === 0
                            ? <span>Аппликатуры такого аккорда автоматически не нашлись. Пожалуйста, поищите вручную.</span>
                            : fields.pictures.map((picture, i) => <img key={i} className="pic" src={`data:image/gif;base64,${picture}`} />)}
                </div>
                <div className="d-flex border-top py-2 px-3">
                    <a href="/fingerings" ref={inlineEventAttribute('onclick', "document.querySelector('#fingering-title').click(); return true;")} className="text-body ms-auto">
                        Смотреть все{' '}
                        <i className="ph-arrow-circle-right ms-1"></i>
                    </a>
                </div>
            </div>
        </li>
    );
}
