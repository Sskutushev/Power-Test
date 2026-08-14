// "Обновлено N назад", ticking on the client.
//
// A server-side timer would push a render over the circuit every few seconds for a label nobody is
// watching closely. The element carries the timestamp as an attribute and the browser counts on its own.

const FORMATTER = new Intl.RelativeTimeFormat('ru-RU', { numeric: 'auto' });
let timer = null;

export function start() {
    stop();
    update();
    timer = window.setInterval(update, 10000);
    document.addEventListener('visibilitychange', onVisibility);
}

export function stop() {
    if (timer) {
        window.clearInterval(timer);
        timer = null;
    }

    document.removeEventListener('visibilitychange', onVisibility);
}

function onVisibility() {
    if (!document.hidden) {
        update();
    }
}

function update() {
    for (const element of document.querySelectorAll('[data-updated-at]')) {
        const stamp = Date.parse(element.dataset.updatedAt);
        if (Number.isNaN(stamp)) {
            continue;
        }

        element.textContent = describe((Date.now() - stamp) / 1000);
    }
}

function describe(seconds) {
    if (seconds < 45) {
        return 'только что';
    }

    if (seconds < 3600) {
        return FORMATTER.format(-Math.round(seconds / 60), 'minute');
    }

    if (seconds < 86400) {
        return FORMATTER.format(-Math.round(seconds / 3600), 'hour');
    }

    return FORMATTER.format(-Math.round(seconds / 86400), 'day');
}
