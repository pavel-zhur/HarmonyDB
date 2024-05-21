export function Clicked() {
    if (window.getComputedStyle(document.getElementById('mobileclosebutton')).display == 'inline-flex') {
        document.getElementById('mobileclosebutton').click();
    }
}