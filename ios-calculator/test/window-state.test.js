// Размер и место окна между запусками (window-state.js). Файл одинаковый в
// calcpro-glass и ios-calculator.
//
// Главное здесь не «запомнило ли», а «что бы ни лежало в файле, окно откроется там,
// где его видно и можно взять за заголовок»: монитор могли отключить, разрешение
// уменьшить, файл обрезать или поправить руками.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const fc = require('fast-check');
const WS = require('../window-state.js');

const OPTS = { width: 320, height: 640, minWidth: 280, minHeight: 500 };
const FULL_HD = { x: 0, y: 0, width: 1920, height: 1040 };   // 1080 минус панель задач
const RIGHT = { x: 1920, y: 0, width: 2560, height: 1400 };   // второй монитор справа

const inside = (w, a) => w.x >= a.x && w.y >= a.y && w.x + w.width <= a.x + a.width && w.y + w.height <= a.y + a.height;

test('no file: the default size, centred by Electron', () => {
  assert.deepEqual(WS.restore(null, [FULL_HD], OPTS), { width: 320, height: 640, maximized: false });
});

test('a window saved on screen opens exactly where it was', () => {
  const saved = { x: 100, y: 200, width: 400, height: 700, maximized: false };
  assert.deepEqual(WS.restore(saved, [FULL_HD], OPTS), saved);
});

test('a window on the second monitor stays there while the monitor is connected', () => {
  const saved = { x: 2500, y: 300, width: 360, height: 720, maximized: false };
  assert.deepEqual(WS.restore(saved, [FULL_HD, RIGHT], OPTS), saved);
});

test('the second monitor unplugged: centred on the main one, the size is kept', () => {
  const saved = { x: 2500, y: 300, width: 360, height: 720, maximized: false };
  assert.deepEqual(WS.restore(saved, [FULL_HD], OPTS), { width: 360, height: 720, maximized: false });
});

test('a window partly past the edge is pulled back whole onto its screen', () => {
  const got = WS.restore({ x: 1800, y: 900, width: 400, height: 700 }, [FULL_HD], OPTS);
  assert.deepEqual(got, { x: 1520, y: 340, width: 400, height: 700, maximized: false });
});

test('a window whose title bar is above the screen is not trusted', () => {
  // Видны только нижние пиксели - за заголовок не взяться.
  assert.equal(WS.restore({ x: 100, y: -700, width: 400, height: 700 }, [FULL_HD], OPTS).x, undefined);
});

test('on a screen lower than the minimum window the title bar stays on the screen', () => {
  // Контрпример fast-check: окно 280x500 на экране 640x481 прижималось низом, и
  // заголовок оказывался на y = -19, выше экрана.
  const small = [{ x: 0, y: 0, width: 640, height: 480 }, { x: 0, y: 0, width: 640, height: 481 }];
  const got = WS.restore({ x: 0, y: 443, width: 0, height: 0 }, small, OPTS);
  assert.deepEqual(got, { x: 0, y: 0, width: 280, height: 500, maximized: false });
  assert.deepEqual(WS.restore(got, small, OPTS), got);
});

test('smaller than the minimum is raised to it, larger than the screen is cut to it', () => {
  assert.deepEqual(WS.restore({ width: 10, height: 10 }, [FULL_HD], OPTS), { width: 280, height: 500, maximized: false });
  const huge = WS.restore({ x: 0, y: 0, width: 9000, height: 9000 }, [FULL_HD], OPTS);
  assert.deepEqual(huge, { x: 0, y: 0, width: 1920, height: 1040, maximized: false });
});

test('maximized is remembered only as a real true', () => {
  assert.equal(WS.restore({ x: 0, y: 0, width: 400, height: 600, maximized: true }, [FULL_HD], OPTS).maximized, true);
  assert.equal(WS.restore({ x: 0, y: 0, width: 400, height: 600, maximized: 'yes' }, [FULL_HD], OPTS).maximized, false);
});

test('garbage instead of numbers gives the default', () => {
  for (const bad of [{}, [], 'x', 42, { width: '400', height: 600 }, { width: NaN, height: 600 }, { width: Infinity, height: 1 }]) {
    assert.deepEqual(WS.restore(bad, [FULL_HD], OPTS), { width: 320, height: 640, maximized: false }, JSON.stringify(bad));
  }
});

// --- Свойства на случайных экранах и случайном содержимом файла ---

const areaArb = fc.record({
  x: fc.integer({ min: -5000, max: 5000 }), y: fc.integer({ min: -3000, max: 3000 }),
  width: fc.integer({ min: 640, max: 5000 }), height: fc.integer({ min: 480, max: 3000 }),
});
// Экраны не перекрываются, как у настоящих мониторов: стоят в ряд слева направо, со
// случайным сдвигом по высоте. На перекрывающихся «экранах» у окна на стыке два равных
// претендента, и свойство повторного восстановления теряет смысл.
const screensArb = fc.array(areaArb, { minLength: 1, maxLength: 4 }).map((list) => {
  let x = list[0].x;
  return list.map((a) => { const placed = { ...a, x }; x += a.width; return placed; });
});
const savedArb = fc.oneof(
  fc.anything(),
  fc.record({
    x: fc.oneof(fc.integer({ min: -20000, max: 20000 }), fc.double(), fc.constant(undefined)),
    y: fc.oneof(fc.integer({ min: -20000, max: 20000 }), fc.double(), fc.constant(undefined)),
    width: fc.oneof(fc.integer({ min: -100, max: 10000 }), fc.double()),
    height: fc.oneof(fc.integer({ min: -100, max: 10000 }), fc.double()),
    maximized: fc.anything(),
  }, { requiredKeys: [] }),
);

test('whatever the file holds, the window has a sane size and, if placed, its title bar on a screen', () => {
  fc.assert(fc.property(savedArb, screensArb, (saved, screens) => {
    const w = WS.restore(saved, screens, OPTS);
    assert.ok(Number.isInteger(w.width) && Number.isInteger(w.height));
    assert.ok(w.width >= OPTS.minWidth && w.height >= OPTS.minHeight);
    assert.equal(typeof w.maximized, 'boolean');
    assert.equal(w.x === undefined, w.y === undefined);
    if (w.x !== undefined) {
      assert.ok(Number.isInteger(w.x) && Number.isInteger(w.y));
      // Окно целиком на одном из экранов (если помещается на нём по размеру), а левый
      // верхний угол - то есть заголовок - на экране всегда.
      assert.ok(screens.some((a) => inside(w, a) || w.width > a.width || w.height > a.height));
      assert.ok(screens.some((a) => w.x >= a.x && w.y >= a.y && w.x < a.x + a.width && w.y + WS.GRIP_HEIGHT <= a.y + a.height));
    }
  }), { numRuns: 3000 });
});

test('restoring what was restored changes nothing', () => {
  fc.assert(fc.property(savedArb, screensArb, (saved, screens) => {
    const once = WS.restore(saved, screens, OPTS);
    if (once.x === undefined) return;
    assert.deepEqual(WS.restore(once, screens, OPTS), once);
  }), { numRuns: 2000 });
});

test('a window that fitted its screen comes back unchanged', () => {
  fc.assert(fc.property(areaArb, fc.integer({ min: 280, max: 640 }), fc.integer({ min: 500, max: 480 + 160 }),
    fc.nat(), fc.nat(), fc.boolean(), (a, width, height, dx, dy, maximized) => {
      fc.pre(width <= a.width && height <= a.height);
      const saved = { x: a.x + (dx % (a.width - width + 1)), y: a.y + (dy % (a.height - height + 1)), width, height, maximized };
      assert.deepEqual(WS.restore(saved, [a], OPTS), saved);
    }), { numRuns: 2000 });
});

// --- Файл ---

test('save then load gives back the same state, and the directory is created', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'ws-'));
  const file = path.join(dir, 'nested', 'window-state.json');
  const state = { x: 10, y: 20, width: 300, height: 600, maximized: true };
  assert.equal(WS.save(file, state), true);
  assert.deepEqual(WS.load(file), state);
  assert.equal(fs.existsSync(file + '.tmp'), false);
  fs.rmSync(dir, { recursive: true, force: true });
});

test('a missing, empty or cut-off file reads as nothing, and the window opens by default', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'ws-'));
  const file = path.join(dir, 'window-state.json');
  assert.equal(WS.load(file), null);
  for (const text of ['', '{"x": 10, "wid', 'null']) {
    fs.writeFileSync(file, text);
    assert.deepEqual(WS.restore(WS.load(file), [FULL_HD], OPTS), { width: 320, height: 640, maximized: false }, text);
  }
  fs.rmSync(dir, { recursive: true, force: true });
});

test('a failed write returns false instead of throwing', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'ws-'));
  assert.equal(WS.save(dir, { width: 1, height: 1 }), false); // вместо файла - каталог
  fs.rmSync(dir, { recursive: true, force: true });
});

test('capture keeps the normal bounds of a maximized window', () => {
  const win = { getNormalBounds: () => ({ x: 5, y: 6, width: 300, height: 600 }), getBounds: () => ({ x: 0, y: 0, width: 1920, height: 1040 }), isMaximized: () => true };
  assert.deepEqual(WS.capture(win), { x: 5, y: 6, width: 300, height: 600, maximized: true });
});
