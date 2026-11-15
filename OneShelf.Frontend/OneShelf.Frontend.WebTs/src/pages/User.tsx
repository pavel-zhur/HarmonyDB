// The original: Pages/User.razor.
//
// [SupplyParameterFromQuery] bool Unauthorized: the "Unauthorized" query parameter (its name in any case), parsed as
// bool.Parse parses ("true"/"false" in any case, with surrounding white space); false when it is absent; a value
// that is not a boolean is an error, as Blazor's query parameter supplier throws.
// window.useCompatMode and localStorage "forceCompatMode" are read as the original reads them through SpawnDev's
// BlazorJSRuntime (the compatible build itself is gone, see PORTING.md, Departures).

import { useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { blazorChecked, stringAttributes } from '../framework/attributes';
import { blazorEventHandler } from '../framework/blazorEventHandler';
import { combineRefs } from '../framework/elementRefs';
import { navigationManager } from '../framework/navigation';
import { PageTitle } from '../framework/PageTitle';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { services } from '../services/registry';

/** [SupplyParameterFromQuery] bool: the parameter's value from the query, false when absent. */
function getBoolFromQuery(search: string, name: string): boolean {
    let value: string | null = null;
    for (const [key, v] of new URLSearchParams(search)) {
        if (key.toLowerCase() === name.toLowerCase()) {
            value = v;
            break;
        }
    }

    if (value === null) return false;

    switch (value.trim().toLowerCase()) {
        case 'true':
            return true;
        case 'false':
            return false;
        default:
            throw new InvalidOperationException(`Cannot parse the value '${value}' as type 'System.Boolean' for '${name}'.`);
    }
}

export function User() {
    const identityProvider = services.identityProvider;
    const stateHasChanged = useStateHasChanged();
    const location = useLocation();

    const unauthorized = getBoolFromQuery(location.search, 'Unauthorized');

    // OnInitializedAsync
    const fields = useRef<{ title: string | null; isCompatMode: boolean; forceCompatMode: boolean } | null>(null);
    fields.current ??= {
        title: identityProvider.identity?.title ?? null,
        isCompatMode: (window as unknown as { useCompatMode?: unknown }).useCompatMode === true,
        forceCompatMode: localStorage.getItem('forceCompatMode') === '1',
    };
    const { title, isCompatMode, forceCompatMode } = fields.current;

    const logout = async (): Promise<void> => {
        await identityProvider.set(null);
        navigationManager.navigateTo('/');
    };

    const switchForceCompatMode = (): void => {
        if (forceCompatMode) {
            localStorage.removeItem('forceCompatMode');
        } else {
            localStorage.setItem('forceCompatMode', '1');
        }

        window.location.reload();
    };

    return (
        <>
            <PageTitle>Профиль</PageTitle>

            <h1>Добрый день, {title}</h1>

            {!unauthorized && (
                <div className="mb-3">Вы можете присоединяться к клубам, напишите админам.</div>
            )}

            {identityProvider.identity !== null
                ? <button className="btn btn-primary mb-3" onClick={blazorEventHandler(stateHasChanged, logout)}>Выйти</button>
                : (
                    <>
                        <h1>Вам бы войти бы:</h1>{' '}
                        <button className="btn btn-primary mb-3" onClick={blazorEventHandler(stateHasChanged, () => { identityProvider.ensureAuthorized(); })}>Войти</button>
                    </>
                )}

            {unauthorized && (
                <div className="mb-3">Может быть, вы видите это сообщение по ошибке. Если вам кажется, что доступ должен быть, тем более, напишите, пожалуйста. Спасибо.</div>
            )}

            <h3>Совместимость</h3>{' '}
            <div className="mb-3">Это для случаев если приложение не загружается с первого раза. Не включайте режим совместимости, если всё работает.</div>{' '}
            <div>
                <label>
                    <input type="checkbox" ref={combineRefs(stringAttributes({ disabled: 'disabled' }), blazorChecked(isCompatMode))} />{' '}
                    Совместимость сейчас{' '}
                </label>
            </div>{' '}
            <div>
                <label>
                    <input type="checkbox" ref={combineRefs(stringAttributes({ disabled: 'disabled' }), blazorChecked(forceCompatMode))} />{' '}
                    Совместимость всегда{' '}
                    <button onClick={blazorEventHandler(stateHasChanged, switchForceCompatMode)}>{forceCompatMode ? 'Выключить' : 'Включить'}</button>
                </label>
            </div>
        </>
    );
}
