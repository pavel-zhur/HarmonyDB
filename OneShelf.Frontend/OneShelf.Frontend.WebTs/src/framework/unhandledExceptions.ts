// What happens to an exception the application does not catch, as in the original (Blazor WebAssembly, .NET 9;
// established with a Blazor WebAssembly 9.0 test application in a browser):
//
// - In a component — an event handler (at once or from its task), a lifecycle method, rendering, an event handler of a
//   service called from those — the renderer catches it and logs it (WebAssemblyRenderer.HandleException): Critical,
//   "Unhandled exception rendering component: {Message}", category WebAssemblyRenderer, event 100
//   "ExceptionRenderingComponent". A Critical message shows the error bar (see logging.ts) and goes to Application
//   Insights as an exception, as every logged exception does. The application goes on. A failed task of a component's
//   event handler or lifecycle method is logged so too; a cancelled one (OperationCanceledException) is not. Here:
//   handleRendererException, for an exception thrown by an event handler (eventDelegator.ts), the task of an event
//   handler or a lifecycle method (errorHandledTask), an exception thrown while rendering or in an effect
//   (ComponentErrorBoundary), and one thrown by the application's code and not caught elsewhere (window "error": the
//   application's timers and listeners). The browser does not report these itself (Blazor's renderer leaves nothing
//   uncaught), and Application Insights' own window.onerror tracking does not see them (it sees only what .NET never
//   caught).
// - In an `async void` method (whatever called it): the .NET runtime prints the exception as unhandled, shows the error
//   bar, and exits (asyncVoid).
// - In a task nobody awaits (`_ = Task`): nothing.
// - A JavaScript promise rejected and not handled (e.g. sw-registrator.js's when the service worker cannot be
//   registered, or a theme script's call back into .NET that failed): not the application's; Blazor takes no such
//   rejection as its own, and the browser reports it. So no "unhandledrejection" listener here: every task of the
//   application's that the original would log reaches handleRendererException through the paths above.
// - Thrown by the page's other scripts (the theme's, index.html's, the original's JavaScript modules): .NET does not
//   see it; the browser reports it as uncaught, and Application Insights tracks it.

import { OperationCanceledException } from '../lib/common/dotnet/exceptions';
import { showErrorNotification } from './errorUi';
import { exceptionToString, LogLevel, writeLog } from './logging';

const loggedExceptions = new WeakSet<object>();

/** WebAssemblyRenderer.HandleException: an unhandled exception in a component. */
export function handleRendererException(exception: unknown): void {
    // React reports some render errors twice in development (to the window and to the boundary): logged once.
    if (typeof exception === 'object' && exception !== null) {
        if (loggedExceptions.has(exception)) return;
        loggedExceptions.add(exception);
    }

    const message = exception instanceof Error ? exception.message : String(exception);
    writeLog({
        logLevel: LogLevel.Critical,
        category: 'Microsoft.AspNetCore.Components.WebAssembly.Rendering.WebAssemblyRenderer',
        eventId: 100,
        eventName: 'ExceptionRenderingComponent',
        message: `Unhandled exception rendering component: ${message}`,
        exception,
        state: [['Message', message], ['{OriginalFormat}', 'Unhandled exception rendering component: {Message}']],
    });
}

/**
 * A component's task the renderer awaits (Renderer.GetErrorHandledTask, ComponentBase's awaits of its lifecycle
 * methods and event handlers): when it fails, the renderer logs the exception (handleRendererException); when it is
 * cancelled (an OperationCanceledException, which makes a .NET task cancelled), nothing is logged. The returned
 * promise never rejects.
 */
export function errorHandledTask(task: Promise<unknown>): Promise<void> {
    return task.then(() => undefined, (exception: unknown) => {
        if (exception instanceof OperationCanceledException) return;
        handleRendererException(exception);
    });
}

/**
 * An `async void` method: runs the body; an exception from it (also one thrown before its first await) is not the
 * caller's, it is unhandled in the .NET runtime, which writes it to the console as below and shows the error bar.
 */
export function asyncVoid(body: () => Promise<void>): void {
    body().catch(unhandledInRuntime);
}

function unhandledInRuntime(exception: unknown): void {
    console.warn('[MONO] Process terminated.');
    console.warn('[MONO] ThreadPool.BackgroundJobHandler failed');
    // The runtime's error output (Blazor's dotNetCriticalError): an empty line, then the exception, line by line.
    console.error('(null)');
    showErrorNotification();
    console.error('Unhandled Exception:');
    for (const line of exceptionToString(exception).split('\n')) {
        console.error(line);
    }
}

/** Whether a script is the application's own (its bundle; in development, the modules Vite serves). */
function isApplicationScript(url: string): boolean {
    if (url === '') return false;
    const scriptUrl = new URL(url, document.baseURI);
    if (scriptUrl.origin !== location.origin) return false;
    if (import.meta.env.DEV) {
        return /^\/(src|node_modules|@[a-z-]+)\//.test(scriptUrl.pathname) || scriptUrl.pathname === '/@react-refresh';
    }

    return scriptUrl.pathname.startsWith(new URL('.', import.meta.url).pathname);
}

/** Called once, before the application starts. */
export function handleUnhandledExceptions(): void {
    // window.onerror is already Application Insights' (its snippet in index.html); it is kept for the other scripts.
    const previousOnError = window.onerror;
    window.onerror = function (message, source, lineno, colno, error) {
        if (isApplicationScript(source ?? '')) {
            handleRendererException(error ?? message);
            return true;
        }

        return previousOnError?.call(window, message, source, lineno, colno, error) ?? false;
    };
}
