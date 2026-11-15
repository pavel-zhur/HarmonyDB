// The original: Pages/Tuner.razor.
//
// Tuner.init() is the static Tuner.Init(): a click that leads here (the menu, the main page) calls it, and the page,
// 100 ms after its first render, starts at once if it was called (a "catcher" value matches), or watches for it every
// 20 ms for 4 seconds; otherwise it shows "Запустить". The tuner itself is Resources/Tuner.html in an iframe's srcdoc.
// The timer, like the original's, is not stopped when the page goes away before it starts; Dispose stops it.

import { useEffect, useRef } from 'react';
import { delay, Timer } from '../dotnet/tasks';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { DateTime } from '../lib/common/dotnet/DateTime';
import { newGuid, type Guid } from '../lib/common/dotnet/Guid';
import { getTunerHtml } from '../resources/Tuner';
import './Tuner.scoped.scss';
import { errorHandledTask } from '../framework/unhandledExceptions';

let cache: string | null = null;

let initializationCatcherValue: Guid = newGuid();
let initializationCatcher: Guid | null = null;

export function Tuner() {
    const stateHasChanged = useStateHasChanged();

    const fields = useRef({
        started: false,
        loading: true,
        timer: null as Timer | null,
    }).current;

    const dispose = (): void => {
        console.log('Disposing timer.');
        fields.timer?.dispose();
        fields.timer = null;
    };

    const load = (): void => {
        fields.loading = false;
        fields.started = initializationCatcher === initializationCatcherValue;
        if (fields.started) {
            initializationCatcherValue = newGuid();
        } else {
            const started = DateTime.now();
            fields.timer = new Timer(() => {
                // (DateTime.Now - started).TotalSeconds
                if (Number(DateTime.now().ticks - started.ticks) / 10_000_000 > 4) {
                    dispose();
                    return;
                }

                if (initializationCatcher === initializationCatcherValue) {
                    fields.started = true;
                    stateHasChanged();
                    dispose();
                }
            }, 0, 20);
        }
    };

    // OnAfterRenderAsync(firstRender); Dispose
    useEffect(() => {
        void errorHandledTask((async () => {
            await delay(100);
            load();
            stateHasChanged();
        })());

        return () => dispose();
    }, []);

    const getBody = (): string => {
        if (cache !== null) return cache;

        const result = getTunerHtml();
        cache = result;
        return result;
    };

    return (
        <>
            <h3>Тюнер</h3>

            {fields.loading
                ? (
                    <>
                        <div>Загрузка...</div>
                        <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
                    </>
                )
                : fields.started
                    ? <iframe srcDoc={getBody()}></iframe>
                    : <button onClick={blazorEventHandler(stateHasChanged, () => { fields.started = true; })} className="btn btn-primary">Запустить</button>}
        </>
    );
}

/** The static Tuner.Init(). */
Tuner.init = (): void => {
    initializationCatcher = initializationCatcherValue;
};
