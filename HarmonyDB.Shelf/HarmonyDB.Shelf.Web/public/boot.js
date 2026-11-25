// Runs before the app: puts on the theme and the interface's size this browser used last (src/app/appearance.ts
// keeps them under this key), so the first paint is already right. Not in the guest's preview (the app framed by its
// own preview page, src/app/guestMode.ts — the same rule): a guest has no look of the person's.
try {
    var preview = false;
    try {
        preview = window.parent !== window && window.parent.location.origin === location.origin
            && /^\/s\/[^/]+\/guest$/.test(window.parent.location.pathname);
    } catch (e) {
        // framed by another origin: not the app's preview
    }
    var look = preview ? null : JSON.parse(localStorage.getItem('shelf.look') || 'null');
    if (look) {
        if (look.theme === 'light' || look.theme === 'dark') document.documentElement.dataset.theme = look.theme;
        if (look.interfaceScale) document.documentElement.style.setProperty('--ui-scale', String(look.interfaceScale));
        if (look.locale) document.documentElement.lang = look.locale;
    }
} catch (e) {
    // no storage: the defaults
}
