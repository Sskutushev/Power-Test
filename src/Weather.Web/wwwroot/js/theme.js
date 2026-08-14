// Theme persistence. Loaded from <head> without `defer` so the stored theme is applied before the first
// paint; an inline script would have been simpler but is blocked by the app's `script-src 'self'` policy.

const STORAGE_KEY = 'weather-theme';
const ALLOWED = new Set(['auto', 'aurora', 'midnight', 'console']);

export function current() {
    return read();
}

export function apply(theme) {
    const next = ALLOWED.has(theme) ? theme : 'auto';

    try {
        window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
        // Private mode or blocked storage: the choice simply does not survive a reload.
    }

    setAttribute(next);

    return next;
}

function read() {
    try {
        const stored = window.localStorage.getItem(STORAGE_KEY);
        return ALLOWED.has(stored) ? stored : 'auto';
    } catch {
        return 'auto';
    }
}

function setAttribute(theme) {
    if (theme === 'auto') {
        document.documentElement.removeAttribute('data-theme');
    } else {
        document.documentElement.setAttribute('data-theme', theme);
    }
}

setAttribute(read());
