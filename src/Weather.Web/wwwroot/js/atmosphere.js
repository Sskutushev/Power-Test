// Animated 3D backdrop.
//
// A single full-screen WebGL quad renders a volumetric sky: two domain-warped fBm cloud layers at
// different parallax depths, a light source with bloom, and procedural precipitation drawn in three
// depth bands. There is no 3D library involved — a vendored engine would add hundreds of kilobytes for
// one background, so this is ~200 lines of GLSL instead.
//
// The scene is driven by the real forecast: `setMood` receives the mapped WeatherAPI condition, so a
// rainy forecast actually rains. Everything degrades gracefully: no WebGL, a lost context, or
// `prefers-reduced-motion` all fall back to the CSS gradient underneath.

const MOODS = { neutral: 0, clear: 1, cloudy: 2, rain: 3, snow: 4, storm: 5 };
const RENDER_SCALE = 0.6;

const VERTEX_SHADER = `
attribute vec2 aPosition;
void main() {
    gl_Position = vec4(aPosition, 0.0, 1.0);
}`;

const FRAGMENT_SHADER = `
precision highp float;

uniform vec2 uResolution;
uniform float uTime;
uniform float uMood;
uniform float uDark;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 5; octave++) {
        value += amplitude * noise(p);
        p *= 2.02;
        amplitude *= 0.5;
    }
    return value;
}

// Two-pass domain warp: the clouds fold into themselves instead of sliding as a flat texture.
float clouds(vec2 uv, float t, float scale) {
    vec2 q = vec2(fbm(uv * scale + vec2(0.0, t * 0.05)), fbm(uv * scale + vec2(5.2, 1.3)));
    vec2 r = vec2(fbm(uv * scale + 4.0 * q + vec2(1.7 - t * 0.03, 9.2)),
                  fbm(uv * scale + 4.0 * q + vec2(8.3, 2.8 + t * 0.02)));
    return fbm(uv * scale + 4.0 * r);
}

float precipitation(vec2 uv, float t, float depth, float slant, float thickness) {
    vec2 p = uv * depth;
    p.x += p.y * slant;
    p.y += t * (0.6 + depth * 0.25);
    vec2 cell = floor(p);
    vec2 local = fract(p) - 0.5;
    float seed = hash(cell);
    if (seed < 0.86) {
        return 0.0;
    }
    float drop = smoothstep(thickness, 0.0, length(local * vec2(6.0, 1.0)));
    return drop;
}

float flakes(vec2 uv, float t, float depth) {
    vec2 p = uv * depth;
    p.x += sin(p.y * 2.0 + t) * 0.15;
    p.y += t * (0.12 + depth * 0.02);
    vec2 cell = floor(p);
    vec2 local = fract(p) - 0.5;
    float seed = hash(cell);
    if (seed < 0.9) {
        return 0.0;
    }
    return smoothstep(0.16, 0.0, length(local));
}

void main() {
    vec2 uv = gl_FragCoord.xy / uResolution.xy;
    vec2 centered = (gl_FragCoord.xy - 0.5 * uResolution.xy) / uResolution.y;
    float t = uTime;

    vec3 skyTop = mix(vec3(0.85, 0.92, 0.99), vec3(0.03, 0.06, 0.12), uDark);
    vec3 skyBottom = mix(vec3(0.99, 0.97, 0.93), vec3(0.06, 0.11, 0.18), uDark);
    vec3 tint = vec3(0.0, 1.0, 0.88);

    if (uMood > 2.5 && uMood < 3.5) {
        skyTop = mix(vec3(0.71, 0.78, 0.86), vec3(0.04, 0.08, 0.14), uDark);
        skyBottom = mix(vec3(0.85, 0.88, 0.92), vec3(0.05, 0.10, 0.16), uDark);
    } else if (uMood > 3.5 && uMood < 4.5) {
        skyTop = mix(vec3(0.88, 0.92, 0.97), vec3(0.06, 0.09, 0.16), uDark);
        skyBottom = mix(vec3(0.96, 0.97, 1.00), vec3(0.09, 0.13, 0.20), uDark);
    } else if (uMood > 4.5) {
        skyTop = mix(vec3(0.55, 0.58, 0.68), vec3(0.02, 0.03, 0.08), uDark);
        skyBottom = mix(vec3(0.72, 0.74, 0.82), vec3(0.05, 0.06, 0.12), uDark);
    }

    vec3 color = mix(skyBottom, skyTop, smoothstep(0.0, 1.0, uv.y));

    // Light source: high and warm for clear skies, low and cold otherwise.
    vec2 sunPos = vec2(0.42, 0.34 + 0.06 * sin(t * 0.08));
    float sunDistance = length(centered - sunPos);
    vec3 sunColor = mix(vec3(1.0, 0.86, 0.55), tint, step(1.5, uMood));
    float sunCore = smoothstep(0.16, 0.0, sunDistance);
    float sunBloom = smoothstep(0.95, 0.0, sunDistance) * 0.45;
    color += sunColor * (sunCore * 0.55 + sunBloom) * mix(1.0, 0.7, uDark);

    // Far cloud deck, slow and soft.
    float far = clouds(uv + vec2(t * 0.012, 0.0), t, 2.2);
    float farMask = smoothstep(0.35, 0.85, far);
    vec3 farColor = mix(vec3(1.0), vec3(0.30, 0.40, 0.55), uDark);
    color = mix(color, farColor, farMask * 0.35);

    // Near cloud deck: larger parallax offset sells the depth.
    float near = clouds(uv * 1.35 + vec2(t * 0.03, -0.05), t * 1.4, 1.4);
    float nearMask = smoothstep(0.45, 0.9, near);
    float density = 0.18;
    if (uMood > 1.5) { density = 0.42; }
    if (uMood > 4.5) { density = 0.6; }
    vec3 nearColor = mix(vec3(0.98, 0.99, 1.0), vec3(0.18, 0.26, 0.38), uDark);
    color = mix(color, nearColor, nearMask * density);

    // Accent bleed keeps the backdrop tied to the brand colour without turning it into a filter.
    color = mix(color, color * (0.85 + 0.35 * tint), 0.10 + 0.06 * nearMask);

    if (uMood > 2.5 && uMood < 3.5) {
        float rain = precipitation(uv, t * 2.4, 26.0, 0.22, 0.55) * 0.35
                   + precipitation(uv, t * 3.1, 44.0, 0.26, 0.40) * 0.22
                   + precipitation(uv, t * 4.0, 70.0, 0.30, 0.30) * 0.14;
        color += vec3(0.55, 0.72, 0.85) * rain * mix(0.9, 1.4, uDark);
    }

    if (uMood > 3.5 && uMood < 4.5) {
        float snow = flakes(uv, t, 22.0) * 0.7 + flakes(uv, t * 1.3, 38.0) * 0.45 + flakes(uv, t * 1.7, 60.0) * 0.3;
        color += vec3(1.0) * snow * mix(0.8, 1.0, uDark);
    }

    if (uMood > 4.5) {
        float strike = step(0.982, hash(vec2(floor(t * 1.7), 3.0)));
        float flash = strike * exp(-fract(t * 1.7) * 9.0);
        color += vec3(0.75, 0.85, 1.0) * flash * 0.5;
        float rain = precipitation(uv, t * 3.4, 34.0, 0.34, 0.5) * 0.4;
        color += vec3(0.6, 0.7, 0.85) * rain;
    }

    // Vignette plus dithering: without the noise the wide gradients band badly on 8-bit displays.
    color *= 1.0 - 0.35 * pow(length(centered * vec2(0.85, 1.0)), 2.0);
    color += (hash(gl_FragCoord.xy) - 0.5) * 0.012;

    gl_FragColor = vec4(color, 1.0);
}`;

let scene = null;

export function start(canvas, mood) {
    stop();

    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    const gl = canvas.getContext('webgl', { antialias: false, alpha: false, depth: false, powerPreference: 'low-power' })
        ?? canvas.getContext('experimental-webgl');

    if (!gl) {
        return false;
    }

    const program = buildProgram(gl);
    if (!program) {
        return false;
    }

    const buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);

    const position = gl.getAttribLocation(program, 'aPosition');
    gl.enableVertexAttribArray(position);
    gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);
    gl.useProgram(program);

    scene = {
        canvas,
        gl,
        program,
        buffer,
        uniforms: {
            resolution: gl.getUniformLocation(program, 'uResolution'),
            time: gl.getUniformLocation(program, 'uTime'),
            mood: gl.getUniformLocation(program, 'uMood'),
            dark: gl.getUniformLocation(program, 'uDark')
        },
        mood: MOODS[mood] ?? 0,
        frame: 0,
        startedAt: performance.now(),
        reduceMotion,
        onResize: () => resize(scene),
        onVisibility: () => (document.hidden ? pause(scene) : play(scene)),
        onContextLost: event => {
            event.preventDefault();
            stop();
        }
    };

    canvas.addEventListener('webglcontextlost', scene.onContextLost, false);
    window.addEventListener('resize', scene.onResize, { passive: true });
    document.addEventListener('visibilitychange', scene.onVisibility);

    resize(scene);

    if (reduceMotion) {
        draw(scene, 0);
    } else {
        play(scene);
    }

    return true;
}

export function setMood(mood) {
    if (scene) {
        scene.mood = MOODS[mood] ?? 0;
        if (scene.reduceMotion) {
            draw(scene, 0);
        }
    }
}

export function stop() {
    if (!scene) {
        return;
    }

    pause(scene);
    scene.canvas.removeEventListener('webglcontextlost', scene.onContextLost);
    window.removeEventListener('resize', scene.onResize);
    document.removeEventListener('visibilitychange', scene.onVisibility);

    const { gl, program, buffer } = scene;
    gl.deleteBuffer(buffer);
    gl.deleteProgram(program);
    scene = null;
}

function play(instance) {
    if (!instance || instance.frame || instance.reduceMotion) {
        return;
    }

    const loop = now => {
        draw(instance, (now - instance.startedAt) / 1000);
        instance.frame = requestAnimationFrame(loop);
    };

    instance.frame = requestAnimationFrame(loop);
}

function pause(instance) {
    if (instance?.frame) {
        cancelAnimationFrame(instance.frame);
        instance.frame = 0;
    }
}

function draw(instance, seconds) {
    const { gl, uniforms } = instance;
    gl.uniform1f(uniforms.time, seconds);
    gl.uniform1f(uniforms.mood, instance.mood);
    gl.uniform1f(uniforms.dark, isDark() ? 1 : 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
}

function resize(instance) {
    if (!instance) {
        return;
    }

    const { canvas, gl } = instance;
    const width = Math.max(1, Math.floor(canvas.clientWidth * RENDER_SCALE));
    const height = Math.max(1, Math.floor(canvas.clientHeight * RENDER_SCALE));

    canvas.width = width;
    canvas.height = height;
    gl.viewport(0, 0, width, height);
    gl.uniform2f(instance.uniforms.resolution, width, height);

    if (instance.reduceMotion) {
        draw(instance, 0);
    }
}

function isDark() {
    const explicit = document.documentElement.getAttribute('data-theme');
    if (explicit === 'midnight') {
        return true;
    }
    if (explicit) {
        return false;
    }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
}

function buildProgram(gl) {
    const vertex = compile(gl, gl.VERTEX_SHADER, VERTEX_SHADER);
    const fragment = compile(gl, gl.FRAGMENT_SHADER, FRAGMENT_SHADER);

    if (!vertex || !fragment) {
        return null;
    }

    const program = gl.createProgram();
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);

    return gl.getProgramParameter(program, gl.LINK_STATUS) ? program : null;
}

function compile(gl, type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        gl.deleteShader(shader);
        return null;
    }

    return shader;
}
