const context = new AudioContext();

// Signal dampening amount
let dampening = 0.99;

// Returns a AudioNode object that will produce a plucking sound
function pluck(frequency) {
    // We create a script processor that will enable
    // low-level signal sample access
    const pluck = context.createScriptProcessor(4096, 0, 1);

    // N is the period of our signal in samples
    const N = Math.round(context.sampleRate / frequency);

    // y is the signal presently
    const y = new Float32Array(N);
    for (let i = 0; i < N; i++) {
        // We fill this with gaussian noise between [-1, 1]
        y[i] = Math.random() * 2 - 1;
    }

    // This callback produces the sound signal
    let n = 0;
    pluck.onaudioprocess = function (e) {
        // We get a reference to the outputBuffer
        const output = e.outputBuffer.getChannelData(0);

        // We fill the outputBuffer with our generated signal
        for (let i = 0; i < e.outputBuffer.length; i++) {
            // This averages the current sample with the next one
            // Effectively, this is a lowpass filter with a
            // frequency exactly half of sampling rate
            y[n] = (y[n] + y[(n + 1) % N]) / 2;

            // Put the actual sample into the buffer
            output[i] = y[n];

            // Hasten the signal decay by applying dampening.
            y[n] *= dampening;

            // Counting constiables to help us read our current
            // signal y
            n++;
            if (n >= N) n = 0;
        }
    };

    // The resulting signal is not as clean as it should be.
    // In lower frequencies, aliasing is producing sharp sounding
    // noise, making the signal sound like a harpsichord. We
    // apply a bandpass centred on our target frequency to remove
    // these unwanted noise.
    const bandpass = context.createBiquadFilter();
    bandpass.type = "bandpass";
    bandpass.frequency.value = frequency;
    bandpass.Q.value = 1;

    // We connect the ScriptProcessorNode to the BiquadFilterNode
    pluck.connect(bandpass);

    // Our signal would have died down by 2s, so we automatically
    // disconnect eventually to prevent leaking memory.
    setTimeout(() => {
        pluck.disconnect();
    }, 2000);
    setTimeout(() => {
        bandpass.disconnect();
    }, 2000);

    // The bandpass is last AudioNode in the chain, so we return
    // it as the "pluck"
    return bandpass;
}

function strum(bass, notes, stagger = 25) {
    // Reset dampening to the natural state
    dampening = 0.99;

    // Connect our strings to the sink
    const dst = context.destination;

    for (let index = 0; index < bass.length; index++) {
        setTimeout(() => {
            pluck(getFrequency(bass[index])).connect(dst);
        }, stagger * index);
    }

    for (let index = 0; index < notes.length; index++) {
        setTimeout(() => {
            pluck(getFrequency(notes[index])).connect(dst);
        }, stagger * (index + bass.length));
    }
}

function getFrequency(note) {
    // Concert A frequency
    const A = 110;

    return A * Math.pow(2, note / 12);
}

function mute() {
    dampening = 0.89;
}

function playChord(bass, notes) {
    context.resume().then(strum(bass, notes));
}

document.addEventListener("DOMContentLoaded", (event) => {
    $('b.chord[chord-data]').bind('click', x => {
        var chordData = x.target.getAttribute('chord-data');
        var all = JSON.parse($('#parsedChords')[0].value);
        playChord(all[chordData].Bass, all[chordData].Main);
    });
});