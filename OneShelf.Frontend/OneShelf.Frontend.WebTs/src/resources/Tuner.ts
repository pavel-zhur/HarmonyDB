// The original's embedded resource Resources/Tuner.html, which Pages/Tuner.razor reads synchronously
// (Assembly.GetManifestResourceStream("OneShelf.Frontend.Web.Resources.Tuner.html")): here part of the build,
// available at once, as there.

import tunerHtml from './Tuner.html?raw';

/** The text of OneShelf.Frontend.Web.Resources.Tuner.html. */
export function getTunerHtml(): string {
    return tunerHtml;
}
