export function bindAll(objRef) {

    const chords = document.querySelectorAll('b.chord[chord-data]');

    for (let i = 0; i < chords.length; i++) {
        chords[i].onclick = function () {
            objRef.invokeMethodAsync('OnChordClick', chords[i].getAttribute('chord-data'), +chords[i].getAttribute('chord-index'));
        };
    }

}