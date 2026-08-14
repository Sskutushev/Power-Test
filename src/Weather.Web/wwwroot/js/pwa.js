// Progressive web app wiring: register the worker, keep the offline snapshot warm, and surface the
// install prompt on the app's own terms rather than leaving it to the browser's mini-infobar.

let deferredPrompt = null;

export async function register() {
    if (!('serviceWorker' in navigator)) {
        return false;
    }

    try {
        await navigator.serviceWorker.register('/sw.js', { scope: '/' });
        // The Blazor UI reads the forecast server-side through MediatR, so /api/weather is never fetched
        // by the browser during normal use and the offline cache would stay empty. One request per page
        // load fills it; it is served from the application's own cache, so the provider sees nothing.
        await warmOfflineSnapshot();

        return true;
    } catch {
        // A failed registration only costs the offline mode; the app itself is unaffected.
        return false;
    }
}

export function canInstall() {
    return deferredPrompt !== null;
}

export async function install() {
    if (!deferredPrompt) {
        return false;
    }

    const prompt = deferredPrompt;
    deferredPrompt = null;
    prompt.prompt();

    const choice = await prompt.userChoice;

    return choice.outcome === 'accepted';
}

async function warmOfflineSnapshot() {
    try {
        await fetch('/api/weather', { cache: 'no-store' });
    } catch {
        // Offline already, or rate limited: the snapshot simply stays as it was.
    }
}

window.addEventListener('beforeinstallprompt', event => {
    // Suppressing the default banner lets the install offer live in the page, next to the theme controls,
    // instead of appearing as a browser overlay at an arbitrary moment.
    event.preventDefault();
    deferredPrompt = event;
    window.dispatchEvent(new CustomEvent('weather:installable'));
});

window.addEventListener('appinstalled', () => {
    deferredPrompt = null;
});
