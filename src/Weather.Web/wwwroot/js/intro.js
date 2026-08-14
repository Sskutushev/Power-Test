// Intro curtain gate. The curtain is a first-impression screen *and* the honest cold-start loading
// state, so it is shown once per browser session rather than on every render.

const KEY = 'weather-intro-seen';

export function shouldShow() {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
        return false;
    }

    try {
        return window.sessionStorage.getItem(KEY) !== '1';
    } catch {
        return true;
    }
}

export function markSeen() {
    try {
        window.sessionStorage.setItem(KEY, '1');
    } catch {
        // Blocked storage only means the intro plays again next navigation.
    }
}
