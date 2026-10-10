/**
 * Car Rental System - High-Fidelity Audio Engine & Sound Synthesizer
 * Uses real audio files (car ignition rev.mp3 & engine stop.wav) placed in wwwroot/sounds/
 * with seamless Web Audio API fallback synthesizer and distinct notification chimes.
 */
(function () {
    let audioCtx = null;
    let isMuted = localStorage.getItem('car_audio_muted') === 'true';

    // Pre-cache audio objects for instantaneous zero-latency playback
    let startAudioCache = null;
    let stopAudioCache = null;

    try {
        startAudioCache = new Audio('sounds/car_ignition_rev.mp3');
        startAudioCache.preload = 'auto';
        stopAudioCache = new Audio('sounds/engine_stop.wav');
        stopAudioCache.preload = 'auto';
    } catch (e) { }

    function getAudioContext() {
        if (!audioCtx) {
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (AudioContextClass) {
                audioCtx = new AudioContextClass();
            }
        }
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume();
        }
        return audioCtx;
    }

    function playAudioFile(url, fallbackFn) {
        if (isMuted) return;
        try {
            const audio = new Audio(url);
            audio.volume = 1.0;
            const p = audio.play();
            if (p !== undefined) {
                p.catch(err => {
                    console.warn("[carAudio] HTML5 audio error, using synthesizer fallback:", err);
                    if (fallbackFn) fallbackFn();
                });
            }
        } catch (err) {
            if (fallbackFn) fallbackFn();
        }
    }

    // Fallback: White/Pink noise generator
    function createNoiseBuffer(ctx, duration) {
        const bufferSize = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let lastOut = 0.0;
        for (let i = 0; i < bufferSize; i++) {
            const white = Math.random() * 2 - 1;
            lastOut = (lastOut + (0.02 * white)) / 1.02;
            data[i] = lastOut * 3.5;
        }
        return buffer;
    }

    // Fallback Synthesizer: Engine Start
    function playSyntheticEngineStart() {
        const ctx = getAudioContext();
        if (!ctx) return;
        const now = ctx.currentTime;

        const comp = ctx.createDynamicsCompressor();
        comp.connect(ctx.destination);
        const masterGain = ctx.createGain();
        masterGain.gain.setValueAtTime(0.8, now);
        masterGain.connect(comp);

        const crankTimes = [0.03, 0.19, 0.35, 0.50];
        const noiseBuf = createNoiseBuffer(ctx, 0.12);

        crankTimes.forEach((t) => {
            const osc = ctx.createOscillator();
            const g = ctx.createGain();
            osc.type = 'sawtooth';
            osc.frequency.setValueAtTime(65, now + t);
            osc.frequency.exponentialRampToValueAtTime(140, now + t + 0.08);
            osc.frequency.exponentialRampToValueAtTime(45, now + t + 0.12);

            g.gain.setValueAtTime(0.001, now + t);
            g.gain.linearRampToValueAtTime(0.28, now + t + 0.03);
            g.gain.exponentialRampToValueAtTime(0.001, now + t + 0.12);

            osc.connect(g);
            g.connect(masterGain);
            osc.start(now + t);
            osc.stop(now + t + 0.13);

            const noise = ctx.createBufferSource();
            noise.buffer = noiseBuf;
            const filter = ctx.createBiquadFilter();
            filter.type = 'bandpass';
            filter.frequency.setValueAtTime(800, now + t);
            filter.Q.setValueAtTime(2.5, now + t);

            const ng = ctx.createGain();
            ng.gain.setValueAtTime(0.22, now + t);
            ng.gain.exponentialRampToValueAtTime(0.001, now + t + 0.11);

            noise.connect(filter);
            filter.connect(ng);
            ng.connect(masterGain);
            noise.start(now + t);
        });

        const ignitionTime = now + 0.64;
        const engineOsc1 = ctx.createOscillator();
        engineOsc1.type = 'sawtooth';
        engineOsc1.frequency.setValueAtTime(60, ignitionTime);
        engineOsc1.frequency.exponentialRampToValueAtTime(235, ignitionTime + 0.35);
        engineOsc1.frequency.exponentialRampToValueAtTime(120, ignitionTime + 0.85);
        engineOsc1.frequency.exponentialRampToValueAtTime(75, ignitionTime + 1.35);

        const engineOsc2 = ctx.createOscillator();
        engineOsc2.type = 'triangle';
        engineOsc2.frequency.setValueAtTime(40, ignitionTime);
        engineOsc2.frequency.exponentialRampToValueAtTime(118, ignitionTime + 0.35);
        engineOsc2.frequency.exponentialRampToValueAtTime(48, ignitionTime + 1.35);

        const engineFilter = ctx.createBiquadFilter();
        engineFilter.type = 'lowpass';
        engineFilter.frequency.setValueAtTime(250, ignitionTime);
        engineFilter.frequency.exponentialRampToValueAtTime(850, ignitionTime + 0.35);
        engineFilter.frequency.exponentialRampToValueAtTime(320, ignitionTime + 1.2);
        engineFilter.Q.setValueAtTime(2.0, ignitionTime);

        const engineGain = ctx.createGain();
        engineGain.gain.setValueAtTime(0.001, ignitionTime);
        engineGain.gain.linearRampToValueAtTime(0.55, ignitionTime + 0.25);
        engineGain.gain.setValueAtTime(0.50, ignitionTime + 0.55);
        engineGain.gain.linearRampToValueAtTime(0.28, ignitionTime + 1.1);
        engineGain.gain.exponentialRampToValueAtTime(0.001, ignitionTime + 1.55);

        engineOsc1.connect(engineFilter);
        engineOsc2.connect(engineFilter);
        engineFilter.connect(engineGain);
        engineGain.connect(masterGain);

        engineOsc1.start(ignitionTime);
        engineOsc2.start(ignitionTime);
        engineOsc1.stop(ignitionTime + 1.6);
        engineOsc2.stop(ignitionTime + 1.6);
    }

    // Fallback Synthesizer: Engine Stop
    function playSyntheticEngineStop() {
        const ctx = getAudioContext();
        if (!ctx) return;
        const now = ctx.currentTime;

        const comp = ctx.createDynamicsCompressor();
        comp.connect(ctx.destination);
        const masterGain = ctx.createGain();
        masterGain.gain.setValueAtTime(0.7, now);
        masterGain.connect(comp);

        const engineOsc = ctx.createOscillator();
        engineOsc.type = 'sawtooth';
        engineOsc.frequency.setValueAtTime(80, now);
        engineOsc.frequency.exponentialRampToValueAtTime(22, now + 0.48);

        const filter = ctx.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.setValueAtTime(320, now);
        filter.frequency.exponentialRampToValueAtTime(90, now + 0.48);

        const engineGain = ctx.createGain();
        engineGain.gain.setValueAtTime(0.35, now);
        engineGain.gain.exponentialRampToValueAtTime(0.001, now + 0.50);

        engineOsc.connect(filter);
        filter.connect(engineGain);
        engineGain.connect(masterGain);

        engineOsc.start(now);
        engineOsc.stop(now + 0.52);

        // Click & Lock chirp
        const chirpTimes = [0.62, 0.79];
        chirpTimes.forEach((t) => {
            const chirpOsc = ctx.createOscillator();
            const chirpGain = ctx.createGain();
            chirpOsc.type = 'sine';
            chirpOsc.frequency.setValueAtTime(2350, now + t);
            chirpOsc.frequency.exponentialRampToValueAtTime(2550, now + t + 0.035);

            chirpGain.gain.setValueAtTime(0.001, now + t);
            chirpGain.gain.linearRampToValueAtTime(0.25, now + t + 0.008);
            chirpGain.gain.exponentialRampToValueAtTime(0.001, now + t + 0.045);

            chirpOsc.connect(chirpGain);
            chirpGain.connect(masterGain);
            chirpOsc.start(now + t);
            chirpOsc.stop(now + t + 0.05);
        });
    }

    window.carAudio = {
        isMuted: function () {
            return isMuted;
        },

        toggleMute: function () {
            isMuted = !isMuted;
            localStorage.setItem('car_audio_muted', isMuted ? 'true' : 'false');
            if (!isMuted) {
                this.playNotification('info');
            }
            return isMuted;
        },

        setMuted: function (muted) {
            isMuted = !!muted;
            localStorage.setItem('car_audio_muted', isMuted ? 'true' : 'false');
        },

        /**
         * Play Car Engine Start (Uses user's car_ignition_rev.mp3 file with fallback)
         */
        playEngineStart: function () {
            if (isMuted) return;
            playAudioFile('sounds/car_ignition_rev.mp3', playSyntheticEngineStart);
        },

        /**
         * Play Car Engine Stop (Uses user's engine_stop.wav file with fallback)
         */
        playEngineStop: function () {
            if (isMuted) return;
            playAudioFile('sounds/engine_stop.wav', playSyntheticEngineStop);
        },

        /**
         * Distinct Musical Tones for Every Notification Type
         */
        playNotification: function (type) {
            if (isMuted) return;
            const ctx = getAudioContext();
            if (!ctx) return;

            const now = ctx.currentTime;
            const masterGain = ctx.createGain();
            masterGain.gain.setValueAtTime(0.65, now);
            masterGain.connect(ctx.destination);

            switch (type) {
                // Urgent Danger / Overdue alert
                case 'danger':
                case 'critical':
                case 'overdue': {
                    const freqs = [880, 1174.66, 880, 1174.66];
                    freqs.forEach((freq, idx) => {
                        const t = now + (idx * 0.09);
                        const osc = ctx.createOscillator();
                        const g = ctx.createGain();
                        osc.type = 'triangle';
                        osc.frequency.setValueAtTime(freq, t);

                        g.gain.setValueAtTime(0.001, t);
                        g.gain.linearRampToValueAtTime(0.32, t + 0.01);
                        g.gain.exponentialRampToValueAtTime(0.001, t + 0.08);

                        osc.connect(g);
                        g.connect(masterGain);
                        osc.start(t);
                        osc.stop(t + 0.085);
                    });
                    break;
                }

                // Warning / Expiry / Due soon
                case 'warning': {
                    const notes = [587.33, 783.99]; // D5, G5
                    notes.forEach((freq, idx) => {
                        const t = now + (idx * 0.14);
                        const osc = ctx.createOscillator();
                        const g = ctx.createGain();
                        osc.type = 'sine';
                        osc.frequency.setValueAtTime(freq, t);

                        g.gain.setValueAtTime(0.001, t);
                        g.gain.linearRampToValueAtTime(0.35, t + 0.015);
                        g.gain.exponentialRampToValueAtTime(0.001, t + 0.32);

                        const harm = ctx.createOscillator();
                        const hg = ctx.createGain();
                        harm.type = 'sine';
                        harm.frequency.setValueAtTime(freq * 2, t);
                        hg.gain.setValueAtTime(0.08, t);
                        hg.gain.exponentialRampToValueAtTime(0.001, t + 0.2);

                        osc.connect(g);
                        harm.connect(hg);
                        g.connect(masterGain);
                        hg.connect(masterGain);

                        osc.start(t);
                        harm.start(t);
                        osc.stop(t + 0.34);
                        harm.stop(t + 0.22);
                    });
                    break;
                }

                // Maintenance / Mechanic tone
                case 'maintenance': {
                    const clickOsc = ctx.createOscillator();
                    const clickGain = ctx.createGain();
                    clickOsc.type = 'triangle';
                    clickOsc.frequency.setValueAtTime(1400, now);
                    clickGain.gain.setValueAtTime(0.2, now);
                    clickGain.gain.exponentialRampToValueAtTime(0.001, now + 0.04);
                    clickOsc.connect(clickGain);
                    clickGain.connect(masterGain);
                    clickOsc.start(now);
                    clickOsc.stop(now + 0.05);

                    const ping = ctx.createOscillator();
                    const pg = ctx.createGain();
                    ping.type = 'sine';
                    ping.frequency.setValueAtTime(659.25, now + 0.05);
                    ping.frequency.exponentialRampToValueAtTime(620, now + 0.3);

                    pg.gain.setValueAtTime(0.001, now + 0.05);
                    pg.gain.linearRampToValueAtTime(0.32, now + 0.06);
                    pg.gain.exponentialRampToValueAtTime(0.001, now + 0.45);

                    ping.connect(pg);
                    pg.connect(masterGain);
                    ping.start(now + 0.05);
                    ping.stop(now + 0.46);
                    break;
                }

                // Success
                case 'success': {
                    const chord = [523.25, 659.25, 783.99, 1046.50];
                    chord.forEach((freq, idx) => {
                        const t = now + (idx * 0.08);
                        const osc = ctx.createOscillator();
                        const g = ctx.createGain();
                        osc.type = 'sine';
                        osc.frequency.setValueAtTime(freq, t);

                        g.gain.setValueAtTime(0.001, t);
                        g.gain.linearRampToValueAtTime(0.3, t + 0.015);
                        g.gain.exponentialRampToValueAtTime(0.001, t + 0.45);

                        osc.connect(g);
                        g.connect(masterGain);
                        osc.start(t);
                        osc.stop(t + 0.48);
                    });
                    break;
                }

                // Car Horn
                case 'horn':
                case 'car_horn': {
                    const hornPitches = [435, 520];
                    const beeps = [0.0, 0.16];
                    beeps.forEach((bTime) => {
                        const t = now + bTime;
                        const duration = (bTime === 0.0) ? 0.09 : 0.15;
                        hornPitches.forEach((freq) => {
                            const osc = ctx.createOscillator();
                            const g = ctx.createGain();
                            osc.type = 'sawtooth';
                            osc.frequency.setValueAtTime(freq, t);

                            const filter = ctx.createBiquadFilter();
                            filter.type = 'lowpass';
                            filter.frequency.setValueAtTime(1400, t);

                            g.gain.setValueAtTime(0.001, t);
                            g.gain.linearRampToValueAtTime(0.18, t + 0.01);
                            g.gain.setValueAtTime(0.18, t + duration - 0.02);
                            g.gain.exponentialRampToValueAtTime(0.001, t + duration);

                            osc.connect(filter);
                            filter.connect(g);
                            g.connect(masterGain);
                            osc.start(t);
                            osc.stop(t + duration + 0.01);
                        });
                    });
                    break;
                }

                // Default info ding
                case 'info':
                case 'contract':
                default: {
                    const notes = [659.25, 987.77];
                    notes.forEach((freq, idx) => {
                        const t = now + (idx * 0.1);
                        const osc = ctx.createOscillator();
                        const g = ctx.createGain();
                        osc.type = 'sine';
                        osc.frequency.setValueAtTime(freq, t);

                        g.gain.setValueAtTime(0.001, t);
                        g.gain.linearRampToValueAtTime(0.28, t + 0.01);
                        g.gain.exponentialRampToValueAtTime(0.001, t + 0.38);

                        osc.connect(g);
                        g.connect(masterGain);
                        osc.start(t);
                        osc.stop(t + 0.4);
                    });
                    break;
                }
            }
        },

        /**
         * Crisp tactile feedback click for numpad buttons
         */
        playKeyClick: function () {
            if (isMuted) return;
            const ctx = getAudioContext();
            if (!ctx) return;
            const now = ctx.currentTime;
            const osc = ctx.createOscillator();
            const g = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(1400, now);
            osc.frequency.exponentialRampToValueAtTime(350, now + 0.03);
            g.gain.setValueAtTime(0.06, now);
            g.gain.exponentialRampToValueAtTime(0.0001, now + 0.035);
            osc.connect(g);
            g.connect(ctx.destination);
            osc.start(now);
            osc.stop(now + 0.04);
        }
    };
})();
