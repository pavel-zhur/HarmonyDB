// The original: Pages/User.razor.
//
// [SupplyParameterFromQuery] bool Unauthorized: the "Unauthorized" query parameter (its name in any case), parsed as
// bool.Parse parses ("true"/"false" in any case, with surrounding white space); false when it is absent; a value
// that is not a boolean is an error, as Blazor's query parameter supplier throws.
// The original's "Совместимость" section is left out with the compatible build (see PORTING.md, Departures).

import { useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { blazorEventHandler } from '../framework/blazorEventHandler';
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
    const fields = useRef<{ title: string | null } | null>(null);
    fields.current ??= {
        title: identityProvider.identity?.title ?? null,
    };
    const { title } = fields.current;

    const logout = async (): Promise<void> => {
        await identityProvider.set(null);
        navigationManager.navigateTo('/');
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
        </>
    );
}
