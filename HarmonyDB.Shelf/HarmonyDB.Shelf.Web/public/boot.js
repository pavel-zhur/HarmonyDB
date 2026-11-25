// Runs before the app: puts on the theme and the interface's size this browser used last (src/app/appearance.ts
// keeps them under this key), so the first paint is already right.
try {
    var look = JSON.parse(localStorage.getItem('shelf.look') || 'null');
    if (look) {
        if (look.theme === 'light' || look.theme === 'dark') document.documentElement.dataset.theme = look.theme;
        if (look.interfaceScale) document.documentElement.style.setProperty('--ui-scale', String(look.interfaceScale));
        if (look.locale) document.documentElement.lang = look.locale;
    }
} catch (e) {
    // no storage: the defaults
}
