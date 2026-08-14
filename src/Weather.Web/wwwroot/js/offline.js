// Renders the last cached dashboard on the offline page.
//
// This is deliberately plain DOM work with no framework: the point of the page is that it works when
// nothing else can, so it must not depend on a circuit, a bundle, or a network round trip.

const CULTURE = 'ru-RU';

render();

async function render() {
    const target = document.getElementById('offline-content');
    if (!target) {
        return;
    }

    const data = await readCachedDashboard();

    if (!data) {
        target.innerHTML = '<p class="muted">Сохранённого прогноза пока нет. Откройте приложение при подключении к сети.</p>';
        return;
    }

    target.innerHTML = '';
    target.append(
        hero(data),
        metrics(data.current),
        hourly(data.hourly ?? []),
        stamp(data.updatedAt)
    );
}

async function readCachedDashboard() {
    try {
        const response = await caches.match('/api/weather');
        return response ? await response.json() : null;
    } catch {
        return null;
    }
}

function hero(data) {
    const section = element('section', 'card current-weather');
    const readout = element('div', 'current-weather__readout');

    readout.append(
        element('p', 'condition', data.current?.condition?.text ?? ''),
        element('p', 'temperature', temperature(data.current?.tempC)),
        element('p', 'muted', `Ощущается как ${temperature(data.current?.feelsLikeC)}`)
    );

    section.append(readout);
    return section;
}

function metrics(current) {
    const grid = element('section', 'metrics-grid');

    const values = [
        ['Влажность', `${Math.round(current?.humidity ?? 0)} %`],
        ['Ветер', `${format(current?.windKph)} км/ч`],
        ['Давление', `${Math.round((current?.pressureMb ?? 0) * 0.750062)} мм рт. ст.`],
        ['УФ-индекс', format(current?.uvIndex)]
    ];

    for (const [label, value] of values) {
        const card = element('article', 'card metric-card');
        card.append(element('span', 'metric-card__label', label), element('strong', 'metric-card__value', value));
        grid.append(card);
    }

    return grid;
}

function hourly(items) {
    const section = element('section', 'section');
    section.append(element('h2', null, 'Почасовой прогноз'));

    const strip = element('div', 'hourly-strip');

    for (const item of items.slice(0, 24)) {
        const card = element('article', 'card hourly-card');
        card.append(
            element('span', 'hourly-card__time', time(item.localTime)),
            element('strong', 'hourly-card__temp', short(item.tempC)),
            element('span', 'hourly-card__rain', `${Math.round(item.chanceOfRain ?? 0)} %`)
        );
        strip.append(card);
    }

    section.append(strip);
    return section;
}

function stamp(updatedAt) {
    const date = new Date(updatedAt);
    const text = Number.isNaN(date.valueOf())
        ? 'Время последнего обновления неизвестно.'
        : `Сохранено ${date.toLocaleString(CULTURE, { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })}.`;

    return element('p', 'muted', text);
}

function element(tag, className, text) {
    const node = document.createElement(tag);

    if (className) {
        node.className = className;
    }

    if (text !== undefined) {
        node.textContent = text;
    }

    return node;
}

function temperature(value) {
    return typeof value === 'number' ? `${value > 0 ? '+' : ''}${value.toFixed(1).replace('.', ',')} °C` : '—';
}

function short(value) {
    return typeof value === 'number' ? `${value > 0 ? '+' : ''}${Math.round(value)}°` : '—';
}

function format(value) {
    return typeof value === 'number'
        ? new Intl.NumberFormat(CULTURE, { maximumFractionDigits: 1 }).format(value)
        : '—';
}

function time(value) {
    const date = new Date(value);
    return Number.isNaN(date.valueOf())
        ? '—'
        : date.toLocaleTimeString(CULTURE, { hour: '2-digit', minute: '2-digit' });
}
