// What Blazor does around a component's event handler (`@onclick="Handler"`, `@oninput="..."`; ComponentBase's
// HandleEventAsync): it calls the handler, renders the component when the handler returns, and, when the handler
// returned a task that has not completed, renders it again when the task completes. An exception from the handler is
// the renderer's to log: thrown at once, it leaves the handler's dispatch (eventDelegator.ts logs it); from the task,
// it is logged when the task fails (no render then), and a cancelled task is neither logged nor rendered after.
//
//     <button onClick={blazorEventHandler(stateHasChanged, () => logout())}>

import { errorHandledTask } from './unhandledExceptions';

export function blazorEventHandler<TArgs extends unknown[]>(
    stateHasChanged: () => void,
    handler: (...args: TArgs) => void | Promise<unknown>,
): (...args: TArgs) => void {
    return (...args: TArgs) => {
        const result = handler(...args);
        stateHasChanged();
        if (result instanceof Promise) {
            void errorHandledTask(result.then(() => stateHasChanged()));
        }
    };
}
