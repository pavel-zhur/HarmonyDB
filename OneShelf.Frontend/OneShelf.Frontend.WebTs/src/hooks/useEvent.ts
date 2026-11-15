// Following a service's event from a component, as a Razor component does with
//
//     protected override void OnInitialized() => Service.SomethingChanged += OnSomethingChanged;
//     public void Dispose() => Service.SomethingChanged -= OnSomethingChanged;
//
// useEvent(services.x.somethingChanged, handler) subscribes when the component appears (before anything else can
// run after its first render) and unsubscribes when it goes. The handler may change between renders; the latest one
// is called. For `event Func<..., Task>` (AsyncDelegateEvent) the handler returns a promise, which the service awaits.
//
// A handler that only re-renders (`Service.X += StateHasChanged`) is useRenderOnEvent(services.x.change).

import { useLayoutEffect, useRef } from 'react';
import type { AsyncDelegateEvent, DelegateEvent } from '../dotnet/events';
import { useStateHasChanged } from './useStateHasChanged';

export function useEvent<TArgs extends unknown[]>(event: DelegateEvent<TArgs>, handler: (...args: TArgs) => void): void {
    const latest = useRef(handler);
    latest.current = handler;

    useLayoutEffect(() => {
        const subscribed = (...args: TArgs) => latest.current(...args);
        event.add(subscribed);
        return () => event.remove(subscribed);
    }, [event]);
}

export function useAsyncEvent<TArgs extends unknown[]>(event: AsyncDelegateEvent<TArgs>, handler: (...args: TArgs) => Promise<void>): void {
    const latest = useRef(handler);
    latest.current = handler;

    useLayoutEffect(() => {
        const subscribed = (...args: TArgs) => latest.current(...args);
        event.add(subscribed);
        return () => event.remove(subscribed);
    }, [event]);
}

/** `Service.X += StateHasChanged;` (and `-=` on Dispose). */
export function useRenderOnEvent<TArgs extends unknown[]>(event: DelegateEvent<TArgs>): void {
    const stateHasChanged = useStateHasChanged();
    useEvent(event, () => stateHasChanged());
}
