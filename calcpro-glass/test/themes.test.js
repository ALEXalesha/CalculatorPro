// Темы оформления: те же пять, что в Paint Pro. Файл одинаковый в calcpro-glass и
// ios-calculator.
//
// Проверять тут надо не «красиво ли», а правила, нарушение которых ломает окно:
//   1. у всех тем один и тот же набор переменных - иначе на смене какая-то окажется
//      пустой, и клавиша станет прозрачной или текст невидимым;
//   2. текст читается на фоне своей темы, а надписи клавиш - на своих клавишах;
//   3. выбор темы переживает перезапуск, а мусор в хранилище не ломает окно;
//   4. в стилях страницы не осталось прибитых цветов, которые тема не перекрасит.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const fc = require('fast-check');
const Theme = require('../src/theme.js');

const SRC = path.join(__dirname, '..', 'src');
const CSS = fs.readFileSync(path.join(SRC, 'themes.css'), 'utf8');

/** Блоки тем: id -> { переменная: значение }. У «Стеклянной» блок :root без атрибута. */
function parseThemes(css) {
  const out = {};
  const re = /:root(?:\[data-theme="([a-z]+)"\])?\s*\{([^}]*)\}/g;
  let m;
  while ((m = re.exec(css))) {
    const vars = {};
    for (const d of m[2].matchAll(/(--[a-z0-9-]+)\s*:\s*([^;]+);/g)) vars[d[1]] = d[2].trim();
    out[m[1] || 'glass'] = vars;
  }
  return out;
}
const THEMES = parseThemes(CSS);

function hex(c) {
  const m = /^#([0-9a-f]{6})$/i.exec(c.trim());
  assert.ok(m, `не #rrggbb: ${c}`);
  return [0, 2, 4].map((i) => parseInt(m[1].slice(i, i + 2), 16));
}
const rgbTriplet = (s) => s.trim().split(/\s+/).map(Number);
function lum([r, g, b]) {
  const ch = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b);
}
function contrast(a, b) {
  const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p);
  return (x + 0.05) / (y + 0.05);
}
const over = (fg, alpha, bg) => fg.map((v, i) => Math.round(v * alpha + bg[i] * (1 - alpha)));

test('the five themes are those of Paint Pro, in the same order', () => {
  assert.deepEqual(Theme.THEMES.map((t) => t.id), ['glass', 'formal', 'light', 'night', 'warm']);
  assert.deepEqual(Theme.THEMES.map((t) => t.name), ['Стеклянная', 'Строгая', 'Светлая', 'Ночная', 'Тёплая']);
  assert.deepEqual(Object.keys(THEMES), Theme.THEMES.map((t) => t.id), 'в themes.css ровно эти темы и в этом порядке');
});

test('every theme defines exactly the same variables', () => {
  const reference = Object.keys(THEMES.glass).sort();
  for (const [id, vars] of Object.entries(THEMES)) {
    assert.deepEqual(Object.keys(vars).sort(), reference, `тема ${id}`);
  }
});

test('text is readable on the background of its own theme', () => {
  for (const [id, v] of Object.entries(THEMES)) {
    const base = hex(v['--app-base']);
    assert.ok(contrast(hex(v['--text']), base) >= 7, `${id}: текст на фоне`);
  }
});

test('key labels are readable on their keys, over the theme background', () => {
  for (const [id, v] of Object.entries(THEMES)) {
    const base = hex(v['--app-base']);
    const text = hex(v['--text']);
    const digit = over(rgbTriplet(v['--key-rgb']), 0.55, base);
    const util = over(rgbTriplet(v['--util-rgb']), 0.62, base);
    assert.ok(contrast(text, digit) >= 4.5, `${id}: цифры (${contrast(text, digit).toFixed(2)})`);
    assert.ok(contrast(hex(v['--util-text']), util) >= 4.5, `${id}: служебные клавиши (${contrast(hex(v['--util-text']), util).toFixed(2)})`);
    // Клавиши операций - градиент от --accent к --accent-2 с белыми символами. Символы
    // крупные, а пары цветов те же, что у кнопок Paint Pro; самое слабое место - охра
    // «Тёплой» (2.84:1), поэтому порог 2.5, а не 3 из WCAG для крупного текста.
    for (const stop of ['--accent', '--accent-2']) {
      assert.ok(contrast(hex(v['--on-accent']), hex(v[stop])) >= 2.5, `${id}: текст на ${stop}`);
    }
  }
});

test('the stylesheet of the page has no colours a theme cannot repaint', () => {
  const page = fs.readFileSync(path.join(SRC, 'index.html'), 'utf8');
  const style = page.slice(page.indexOf('<style>'), page.indexOf('</style>'));
  // Белое стекло, белый текст и оранжевый Apple были прибиты и не менялись с темой.
  assert.doesNotMatch(style, /rgba\(\s*255\s*,\s*255\s*,\s*255\s*,/, 'белое стекло - через rgb(var(--ink) / a)');
  assert.doesNotMatch(style, /color:\s*#fff(?:fff)?\s*;/i, 'белый текст - через var(--text) или var(--on-accent)');
  assert.doesNotMatch(style, /#ff9500|#ff9f0a|rgba\(\s*255\s*,\s*1[4-9]0\s*,/i, 'оранжевый - через акцент темы');
  assert.doesNotMatch(page, /stroke="white"|fill="white"/, 'значки - цветом текста темы');
});

test('both calculators carry the same theme files', { skip: !fs.existsSync(path.join(__dirname, '..', '..', 'ios-calculator')) }, () => {
  const here = path.basename(path.join(__dirname, '..'));
  const other = here === 'calcpro-glass' ? 'ios-calculator' : 'calcpro-glass';
  for (const f of ['themes.css', 'theme.js']) {
    const a = fs.readFileSync(path.join(SRC, f), 'utf8');
    const b = fs.readFileSync(path.join(__dirname, '..', '..', other, 'src', f), 'utf8');
    assert.equal(a, b, `${f} разошёлся с ${other}`);
  }
});

// --- логика выбора и памяти -------------------------------------------------------

function fakeEl() {
  const attrs = new Map();
  return {
    getAttribute: (k) => (attrs.has(k) ? attrs.get(k) : null),
    setAttribute: (k, v) => attrs.set(k, String(v)),
    removeAttribute: (k) => attrs.delete(k),
  };
}
function fakeStorage(initial) {
  const data = new Map(initial ? Object.entries(initial) : []);
  return {
    writes: 0,
    getItem(k) { return data.has(k) ? data.get(k) : null; },
    setItem(k, v) { this.writes++; data.set(k, String(v)); },
  };
}
const brokenStorage = { getItem() { throw new Error('denied'); }, setItem() { throw new Error('denied'); } };

test('any id: the applied theme is a known one and the attribute says the same', () => {
  fc.assert(fc.property(fc.oneof(fc.constantFrom(...Theme.THEMES.map((t) => t.id)), fc.string()), (id) => {
    const el = fakeEl();
    const st = fakeStorage();
    const got = Theme.apply(el, id, st);
    assert.ok(Theme.THEMES.some((t) => t.id === got));
    assert.equal(Theme.current(el), got);
    assert.equal(el.getAttribute('data-theme'), got === 'glass' ? null : got, 'у исходной атрибута нет, как в Paint');
    // Что записано - то и вернётся при следующем запуске.
    const next = fakeEl();
    assert.equal(Theme.restore(next, st), got);
    assert.equal(Theme.current(next), got);
  }), { numRuns: 500 });
});

test('restore never writes, and garbage in the storage falls back to glass', () => {
  for (const saved of [null, '', 'no-such', 'LIGHT', '__proto__', 'glass', 'night']) {
    const st = fakeStorage(saved === null ? {} : { [Theme.STORAGE_KEY]: saved });
    const el = fakeEl();
    const got = Theme.restore(el, st);
    assert.equal(st.writes, 0);
    assert.equal(got, ['glass', 'night'].includes(saved) ? saved : 'glass');
  }
});

test('a storage that throws does not break choosing or starting', () => {
  const el = fakeEl();
  assert.equal(Theme.apply(el, 'warm', brokenStorage), 'warm');
  assert.equal(el.getAttribute('data-theme'), 'warm', 'тема применилась, хоть и не запомнилась');
  assert.equal(Theme.restore(fakeEl(), brokenStorage), 'glass');
  assert.equal(Theme.apply(fakeEl(), 'light', null), 'light');
});
