// Blazor's error notification (Web.JS BootErrors.ts, .NET 9): shows index.html's #blazor-error-ui and makes its
// "reload" and "dismiss" links work. Blazor WebAssembly shows it when the application fails to start, on every Critical
// log message (dotNetCriticalError; the renderer logs an unhandled exception in a component so), and on the runtime's
// error output (an unhandled exception in the runtime). See logging.ts and unhandledExceptions.ts.

/** Called once, before the application starts: unhandled exceptions are handled as in the original. */
export { handleUnhandledExceptions as showErrorNotificationOnUnhandledErrors } from './unhandledExceptions';

let hasFailed = false;

export function showErrorNotification(): void {
    const errorUi = document.querySelector<HTMLElement>('#blazor-error-ui');
    if (errorUi) {
        errorUi.style.display = 'block';
    }

    if (!hasFailed) {
        hasFailed = true;
        document.querySelectorAll<HTMLElement>('#blazor-error-ui .reload').forEach(reload => {
            reload.onclick = function (e) {
                location.reload();
                e.preventDefault();
            };
        });

        document.querySelectorAll<HTMLElement>('#blazor-error-ui .dismiss').forEach(dismiss => {
            dismiss.onclick = function (e) {
                const errorUi = document.querySelector<HTMLElement>('#blazor-error-ui');
                if (errorUi) {
                    errorUi.style.display = 'none';
                }

                e.preventDefault();
            };
        });
    }
}
