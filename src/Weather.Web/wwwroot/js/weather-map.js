// Territory forecast map.
//
// Everything here is open source / open data and keyless:
//   - Leaflet 1.9.4 (BSD-2-Clause), vendored under /lib/leaflet so the page needs no CDN;
//   - OpenStreetMap raster tiles (ODbL) for the base map;
//   - RainViewer public radar tiles for the precipitation overlay.
//
// The radar layer is strictly optional: if the RainViewer index cannot be read the map still renders
// with the base layer and the temperature markers.

const RAINVIEWER_INDEX = 'https://api.rainviewer.com/public/weather-maps.json';
const FRAME_INTERVAL_MS = 700;
const instances = new Map();

export function render(container, payload) {
    if (!container || typeof L === 'undefined') {
        return false;
    }

    dispose(container);

    // The attribution overlay is turned off here and rendered in the page footer instead. OpenStreetMap's
    // ODbL licence requires visible credit, not a specific widget, and the corner control was covering
    // the map. Leaflet's own "Leaflet" link is optional and is dropped.
    const map = L.map(container, {
        center: [payload.centerLatitude, payload.centerLongitude],
        zoom: payload.zoom,
        // Wheel zoom is armed only while the pointer is over the map. Leaving it always on would hijack
        // page scrolling whenever the map happens to pass under the cursor.
        scrollWheelZoom: false,
        attributionControl: false
    });

    const armWheelZoom = () => map.scrollWheelZoom.enable();
    const disarmWheelZoom = () => map.scrollWheelZoom.disable();
    container.addEventListener('pointerenter', armWheelZoom);
    container.addEventListener('pointerleave', disarmWheelZoom);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 12,
        minZoom: 3
    }).addTo(map);

    const instance = {
        map,
        container,
        armWheelZoom,
        disarmWheelZoom,
        radarLayers: [],
        frames: [],
        frameIndex: 0,
        timer: null,
        onFrame: payload.onFrame ?? null
    };
    instances.set(container, instance);

    addMarkers(map, payload.points ?? []);
    map.whenReady(() => setTimeout(() => map.invalidateSize(), 0));

    // Fire and forget: a radar failure must not break the map.
    loadRadar(instance).catch(() => { /* base map stays usable without the overlay */ });

    return true;
}

export function isAnimating(container) {
    return Boolean(instances.get(container)?.timer);
}

export function toggleRadarAnimation(container) {
    const instance = instances.get(container);
    if (!instance || instance.frames.length === 0) {
        return false;
    }

    if (instance.timer) {
        stopAnimation(instance);
        return false;
    }

    startAnimation(instance);
    return true;
}

export function dispose(container) {
    const instance = instances.get(container);
    if (!instance) {
        return;
    }

    stopAnimation(instance);
    instance.container.removeEventListener('pointerenter', instance.armWheelZoom);
    instance.container.removeEventListener('pointerleave', instance.disarmWheelZoom);
    instance.map.remove();
    instances.delete(container);
}

function addMarkers(map, points) {
    if (points.length === 0) {
        return;
    }

    const bounds = [];

    for (const point of points) {
        const icon = L.divIcon({
            className: 'map-marker',
            html: markerHtml(point),
            iconSize: [58, 34],
            iconAnchor: [29, 17]
        });

        L.marker([point.latitude, point.longitude], { icon, title: point.name, alt: point.name })
            .addTo(map)
            .bindPopup(popupHtml(point));

        bounds.push([point.latitude, point.longitude]);
    }

    if (bounds.length > 1) {
        map.fitBounds(bounds, { padding: [36, 36] });
    }
}

function markerHtml(point) {
    const temp = formatTemperature(point.tempC);
    return `<span class="map-marker__chip" data-band="${temperatureBand(point.tempC)}">
                <span class="map-marker__temp">${temp}</span>
                <span class="map-marker__name">${escapeHtml(point.name)}</span>
            </span>`;
}

function popupHtml(point) {
    const rows = [
        ['Ощущается', formatTemperature(point.feelsLikeC)],
        ['Ветер', `${formatNumber(point.windKph)} км/ч`],
        ['Влажность', `${point.humidity} %`]
    ];

    const list = rows
        .map(([label, value]) => `<dt>${label}</dt><dd>${escapeHtml(String(value))}</dd>`)
        .join('');

    return `<div class="map-popup">
                <p class="map-popup__title">${escapeHtml(point.name)}</p>
                <p class="map-popup__temp">${formatTemperature(point.tempC)}</p>
                <p class="map-popup__condition">${escapeHtml(point.conditionText ?? '')}</p>
                <dl class="map-popup__grid">${list}</dl>
            </div>`;
}

async function loadRadar(instance) {
    const response = await fetch(RAINVIEWER_INDEX, { cache: 'no-store' });
    if (!response.ok) {
        return;
    }

    const index = await response.json();
    const past = index?.radar?.past ?? [];
    if (past.length === 0) {
        return;
    }

    instance.frames = past.slice(-8).map(frame => ({
        time: frame.time,
        url: `${index.host}${frame.path}/256/{z}/{x}/{y}/4/1_1.png`
    }));

    instance.radarLayers = instance.frames.map(frame => L.tileLayer(frame.url, {
        opacity: 0,
        zIndex: 500,
        maxZoom: 12
    }).addTo(instance.map));

    instance.frameIndex = instance.frames.length - 1;
    showFrame(instance, instance.frameIndex);

    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    if (!reduceMotion) {
        startAnimation(instance);
    }
}

function startAnimation(instance) {
    stopAnimation(instance);
    instance.timer = window.setInterval(() => {
        instance.frameIndex = (instance.frameIndex + 1) % instance.frames.length;
        showFrame(instance, instance.frameIndex);
    }, FRAME_INTERVAL_MS);
}

function stopAnimation(instance) {
    if (instance.timer) {
        window.clearInterval(instance.timer);
        instance.timer = null;
    }
}

function showFrame(instance, index) {
    instance.radarLayers.forEach((layer, position) => layer.setOpacity(position === index ? 0.65 : 0));

    const stamp = instance.frames[index]?.time;
    if (!stamp) {
        return;
    }

    // The timestamp lives in the section header, outside the map container, so it is looked up from
    // the document rather than from the map's own subtree.
    const label = new Date(stamp * 1000).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
    for (const target of document.querySelectorAll('[data-radar-time]')) {
        target.textContent = label;
    }
}

function temperatureBand(value) {
    if (value <= -10) return 'freezing';
    if (value <= 0) return 'cold';
    if (value <= 10) return 'cool';
    if (value <= 20) return 'mild';
    if (value <= 28) return 'warm';
    return 'hot';
}

function formatTemperature(value) {
    const rounded = Math.round(value);
    return rounded > 0 ? `+${rounded}°` : `${rounded}°`;
}

function formatNumber(value) {
    return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}

function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, character => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    }[character]));
}
