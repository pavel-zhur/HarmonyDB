export function Prompt(query) {
    return prompt(query);
}

export function Confirm(query) {
    return confirm(query);
}

export function ScrollToTop() {
    document.querySelector('.content-inner').scrollTo(0, 0);
}