// Desktop interaction for the hourly strip: drag-to-scroll, wheel-to-horizontal, and a hover readout.
//
// The hover readout is handled here rather than with Blazor @onmouseover on purpose: on Blazor Server
// every DOM event is a round trip over the circuit, and a pointer moving across 33 cards would generate
// a burst of them. The card already carries its values as data attributes, so the client can do it alone.

const attached = new WeakMap();

// Puts the hour the visitor is actually in at the left edge, so the first thing they see is now rather
// than a scroll position that happens to start at zero.
export function scrollToNow(strip) {
    const current = strip?.querySelector('[data-now="true"]');
    if (!current) {
        return;
    }

    strip.scrollTo({
        left: Math.max(current.offsetLeft - strip.offsetLeft - 8, 0),
        behavior: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
    });
}

export function attach(strip, readout) {
    if (!strip || attached.has(strip)) {
        return;
    }

    const state = { down: false, startX: 0, startScroll: 0, moved: false };

    const onPointerDown = event => {
        // Left button only, and never on a focusable child.
        if (event.button !== 0) {
            return;
        }

        state.down = true;
        state.moved = false;
        state.startX = event.clientX;
        state.startScroll = strip.scrollLeft;
        strip.setPointerCapture(event.pointerId);
        strip.classList.add('is-dragging');
    };

    const onPointerMove = event => {
        if (state.down) {
            const delta = event.clientX - state.startX;
            if (Math.abs(delta) > 3) {
                state.moved = true;
            }
            strip.scrollLeft = state.startScroll - delta;
            event.preventDefault();
            return;
        }

        updateReadout(event);
    };

    const onPointerUp = event => {
        if (!state.down) {
            return;
        }

        state.down = false;
        strip.classList.remove('is-dragging');

        if (strip.hasPointerCapture(event.pointerId)) {
            strip.releasePointerCapture(event.pointerId);
        }
    };

    // A vertical wheel over a horizontal strip should move it sideways, but only while it still has
    // somewhere to go — otherwise the page stops scrolling under the cursor.
    const onWheel = event => {
        if (event.deltaY === 0 || event.shiftKey) {
            return;
        }

        const max = strip.scrollWidth - strip.clientWidth;
        const next = strip.scrollLeft + event.deltaY;

        if ((next > 0 || event.deltaY > 0) && (next < max || event.deltaY < 0)) {
            strip.scrollLeft = next;
            event.preventDefault();
        }
    };

    const updateReadout = event => {
        if (!readout) {
            return;
        }

        const card = event.target.closest?.('[data-hour]');
        if (!card) {
            return;
        }

        readout.textContent = [
            card.dataset.hour,
            card.dataset.condition,
            card.dataset.temp,
            `ветер ${card.dataset.wind}`,
            `осадки ${card.dataset.rain}`
        ].filter(Boolean).join(' · ');
        readout.dataset.active = 'true';
    };

    const onLeave = () => {
        state.down = false;
        strip.classList.remove('is-dragging');

        if (readout) {
            readout.dataset.active = 'false';
        }
    };

    strip.addEventListener('pointerdown', onPointerDown);
    strip.addEventListener('pointermove', onPointerMove);
    strip.addEventListener('pointerup', onPointerUp);
    strip.addEventListener('pointercancel', onPointerUp);
    strip.addEventListener('pointerleave', onLeave);
    strip.addEventListener('wheel', onWheel, { passive: false });
    // A drag that moved should not also activate whatever was under the cursor.
    strip.addEventListener('click', event => {
        if (state.moved) {
            event.preventDefault();
            event.stopPropagation();
        }
    }, true);

    attached.set(strip, true);
}
