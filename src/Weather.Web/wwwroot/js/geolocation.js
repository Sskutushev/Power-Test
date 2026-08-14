// Opt-in visitor location.
//
// The assignment fixes the location to Moscow, so this never runs on its own: the browser prompt only
// appears after the visitor presses the button. The choice is remembered per browser so the prompt is
// not repeated on every visit, and clearing it is one click away.

const KEY = 'weather-location';
const TIMEOUT_MS = 10000;

export function supported() {
    return typeof navigator !== 'undefined' && 'geolocation' in navigator;
}

export function stored() {
    try {
        const raw = window.localStorage.getItem(KEY);
        if (!raw) {
            return null;
        }

        const value = JSON.parse(raw);
        return isValid(value) ? value : null;
    } catch {
        return null;
    }
}

export function clear() {
    try {
        window.localStorage.removeItem(KEY);
    } catch {
        // Blocked storage only means the choice does not survive a reload.
    }
}

export async function request() {
    if (!supported()) {
        return null;
    }

    const position = await new Promise(resolve => {
        navigator.geolocation.getCurrentPosition(
            result => resolve(result),
            // A denied or failed prompt is a normal outcome, not an error to propagate.
            () => resolve(null),
            { enableHighAccuracy: false, timeout: TIMEOUT_MS, maximumAge: 5 * 60 * 1000 });
    });

    if (!position) {
        return null;
    }

    // Four decimals is roughly 11 metres — plenty for a forecast, and it avoids storing a more precise
    // position than the feature needs.
    const value = {
        latitude: round(position.coords.latitude),
        longitude: round(position.coords.longitude)
    };

    try {
        window.localStorage.setItem(KEY, JSON.stringify(value));
    } catch {
        // Not remembering the choice is harmless.
    }

    return value;
}

function round(value) {
    return Math.round(value * 10000) / 10000;
}

function isValid(value) {
    return value
        && Number.isFinite(value.latitude)
        && Number.isFinite(value.longitude)
        && Math.abs(value.latitude) <= 90
        && Math.abs(value.longitude) <= 180;
}
