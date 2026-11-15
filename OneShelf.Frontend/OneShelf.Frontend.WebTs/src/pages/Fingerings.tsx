// The original: Pages/Fingerings.razor.
//
// The sets of switched/loading notes are HashSet<(string typeUrl, FingeringsNote note)>: here sets of a key made of
// both members.

import { Fragment, useEffect, useRef, type FormEvent } from 'react';
import { delay } from '../dotnet/tasks';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { inlineEventAttribute } from '../framework/inlineEventAttribute';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { getRequired } from '../dotnet/collections';
import { toBase64String } from '../lib/common/dotnet/Convert';
import { isNullOrWhiteSpace, toLowerInvariant, trim } from '../lib/common/dotnet/text';
import { once } from '../lib/common/OneShelf.Common/Extensions';
import type { FingeringsChordType } from '../models/Fingerings/FingeringsChordType';
import type { FingeringsModelIndex } from '../models/Fingerings/FingeringsModelIndex';
import type { FingeringsNote } from '../models/Fingerings/FingeringsNote';
import { services } from '../services/registry';
import './Fingerings.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

/** The (string typeUrl, FingeringsNote note) tuple as a set key. */
function noteKey(typeUrl: string, note: FingeringsNote): string {
    return `${note}:${typeUrl}`;
}

export function Fingerings() {
    const fingeringsProvider = services.fingeringsProvider;
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({
        model: null as FingeringsModelIndex | null,
        loading: new Set<string>(),
        switched: new Set<string>(),
        loading2: new Set<string>(),
        unloading2: new Set<string>(),
        switched2: new Set<string>(),
        search: null as string | null,
    }).current;

    // OnAfterRenderAsync(firstRender)
    useEffect(() => {
        void errorHandledTask((async () => {
            await delay(100);
            fields.model = await fingeringsProvider.getModel();
            stateHasChanged();
        })());
    }, [fields, fingeringsProvider, stateHasChanged]);

    const switch1 = async (typeUrl: string): Promise<void> => {
        if (!fields.switched.delete(typeUrl)) {
            fields.loading.add(typeUrl);
            stateHasChanged();
            await delay(50);
            fields.loading.delete(typeUrl);
            fields.switched.add(typeUrl);
        }
    };

    const switch2 = async (key: string): Promise<void> => {
        if (!fields.switched2.delete(key)) {
            fields.loading2.add(key);
            stateHasChanged();
            await delay(200);
            fields.loading2.delete(key);
            fields.switched2.add(key);
        } else {
            fields.unloading2.add(key);
            stateHasChanged();
            await delay(50);
            fields.unloading2.delete(key);
        }
    };

    const setSearch = (e: FormEvent<HTMLInputElement>): void => {
        let search: string | null = trim(toLowerInvariant(e.currentTarget.value));
        if ([...'abcdefgh'].some(x => search?.startsWith(x) ?? false)) search = search.substring(1);
        fields.search = search;
        stateHasChanged();
    };

    const model = fields.model;
    const search = fields.search;

    const types = model === null
        ? []
        : model.model.types
            .map(x =>
                isNullOrWhiteSpace(search)
                || x.chordText.includes(search!)
                || [...fingeringsProvider.synonymsTextToText].some(([key, value]) => key.includes(search!) && value === x.chordText)
                    ? { x: x as FingeringsChordType | null, shouldRender: true }
                    : { x: null, shouldRender: x.url === 'Cm' })
            .filter(x => x.shouldRender)
            .map(x => x.x);

    return (
        <>
            <h3>Аппликатуры на гитаре</h3>
            <div>
                <input className="form-control my-search mb-2" type="search" placeholder="Поиск..." onInput={blazorEventHandler(stateHasChanged, setSearch)} />{' '}
                В аккордах любой песни нажмите на аккорд, и покажутся его аппликатуры.{' '}
                <br />{' '}
                Или ищите аппликатуры здесь.{' '}
                <br />{' '}
                Тут две большие группы - мажорные и минорные.{' '}
            </div>
            {model === null
                ? (
                    <>
                        <h5 className="mt-4">Загрузка...</h5>
                        <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
                    </>
                )
                : (
                    <>
                        <h5 className="mt-4" id="major">
                            {' '}Мажорные / <a className="my-link my-smaller" ref={inlineEventAttribute('onclick', "document.getElementById('minor').scrollIntoView({behavior:'smooth'})")}>минорные</a>
                        </h5>
                        {types.map((type, typeIndex) => (
                            <Fragment key={typeIndex}>
                                {(type === null || type.url === 'Cm') && (
                                    <h5 className="mt-4" id="minor">
                                        {' '}Минорные / <a className="my-link my-smaller" ref={inlineEventAttribute('onclick', "document.getElementById('major').scrollIntoView({behavior:'smooth'})")}>мажорные</a>
                                    </h5>
                                )}
                                {type !== null && (
                                    <div className="mb-1">
                                        <div>
                                            <span onClick={blazorEventHandler(stateHasChanged, () => switch1(type.url))} className={`${fields.loading.has(type.url) || fields.switched.has(type.url) ? '' : 'collapsed'} my-link`}>
                                                <b dangerouslySetInnerHTML={{ __html: [...once(type.chordHtml), ...fingeringsProvider.synonymsTextToHtmls.get(type.chordText)].join(', ') }}></b>: {type.description}{' '}
                                                <i className="ph-caret-down collapsible-indicator"></i>
                                            </span>
                                        </div>
                                        {fields.switched.has(type.url) && (
                                            <div>
                                                {[...type.notes].flatMap(([note, images]) => fields.switched2.has(noteKey(type.url, note))
                                                    ? images.map((image, i) => ({ note, images, image, isExpanded: true, isLoading: false, isUnloading: false, i, total: images.length }))
                                                    : once({ note, images, image: images[0]!, isExpanded: false, isLoading: fields.loading2.has(noteKey(type.url, note)), isUnloading: fields.unloading2.has(noteKey(type.url, note)), i: 0, total: 0 }))
                                                    .map(({ note, images, image, isExpanded, isLoading, isUnloading, i, total }, index) => (
                                                        <div key={index} className={`pic ${isLoading || isExpanded || isUnloading ? 'my-loading' : ''} my-link`} onClick={blazorEventHandler(stateHasChanged, () => switch2(noteKey(type.url, note)))}>
                                                            <img className="pic" src={`data:image/gif;base64,${toBase64String(getRequired(model.model.pictures, image))}`} />{' '}
                                                            <span className={isExpanded || isLoading || isUnloading ? 'my-bolder' : 'collapsed'}>
                                                                {isExpanded
                                                                    ? <span>{i + 1} / {total}</span>
                                                                    : (
                                                                        <>
                                                                            <span>и еще {images.length - 1}</span>{' '}
                                                                            <span className="ph-caret-down collapsible-indicator"></span>
                                                                        </>
                                                                    )}
                                                            </span>
                                                        </div>
                                                    ))}
                                            </div>
                                        )}
                                    </div>
                                )}
                            </Fragment>
                        ))}
                    </>
                )}
        </>
    );
}
