// The key-press state machine of calc-core.js, driven like a user would.
const test = require('node:test');
const assert = require('node:assert/strict');
const fc = require('fast-check');
const C = require('../src/calc-core.js');

function memoryStorage(initial = {}) {
  const data = { ...initial };
  return {
    data,
    getItem: k => (k in data ? data[k] : null),
    setItem: (k, v) => { data[k] = String(v); },
  };
}

const newCalc = (storage = memoryStorage()) => C.createCalculator({ storage, now: () => 0 });

function press(calc, keys) {
  for (const k of keys) calc.pressKey(k);
  return calc.state;
}

// ---------------------------------------------------------------- random driving
const layoutKeys = new Set();
for (const layout of [C.STD_LAYOUT, C.SCI_LAYOUT, C.FRAC_LAYOUT]) for (const b of layout) layoutKeys.add(b.k);
for (const k of Object.values(C.ALT_KEYS)) layoutKeys.add(k);
['x!', '^', '(', ')'].forEach(k => layoutKeys.add(k));
const DIGITS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'];
const ACTIONS = [...layoutKeys, 'mode:standard', 'mode:scientific', 'mode:fraction', 'mode:toggle',
  'angle', 'mem:add', 'mem:sub', 'mem:store', 'mem:clear', 'mem:recall', 'hist:0', 'hist:3', 'hist:clear'];
const action = fc.oneof(fc.constantFrom(...DIGITS), fc.constantFrom(...DIGITS), fc.constantFrom(...ACTIONS));

function apply(calc, a) {
  if (a.startsWith('mode:')) { const m = a.slice(5); if (m === 'toggle') calc.toggleMode(); else calc.setMode(m); return; }
  if (a === 'angle') { calc.cycleAngleMode(); return; }
  if (a.startsWith('mem:')) {
    ({ add: calc.memAdd, sub: calc.memSub, store: calc.memStore, clear: calc.memClear, recall: calc.memRecall })[a.slice(4)]();
    return;
  }
  if (a === 'hist:clear') { calc.clearHistory(); return; }
  if (a.startsWith('hist:')) { calc.useHistoryEntry(+a.slice(5)); return; }
  calc.pressKey(a);
}

const ERROR_TEXTS = new Set(["Can't divide by 0", 'Overflow', 'Not a number', 'Syntax error', 'Error',
  'Incomplete fraction', 'Division by zero', 'Math error']);

function checkInvariants(s, trail) {
  const where = () => `after ${JSON.stringify(trail)}: ${JSON.stringify({ e: s.expression, d: s.display, h: s.historyLine })}`;
  for (const text of [s.expression, s.display, s.historyLine]) {
    assert.equal(typeof text, 'string', where());
    assert.ok(!/NaN|undefined|Infinity|\[object/.test(text), where());
  }
  assert.ok(!/Overflow/.test(s.expression), 'error text never leaks into the expression ' + where());
  assert.ok(['standard', 'scientific', 'fractions'].includes(s.mode));
  assert.ok(['deg', 'rad', 'grad'].includes(s.angleMode));
  assert.ok(Number.isFinite(s.memory), 'memory is a finite number ' + where());
  assert.ok(s.calcHistory.length <= C.HISTORY_LIMIT);
  if (s.result !== null) assert.ok(Number.isFinite(s.result), where());
  if (s.hasError) assert.ok(ERROR_TEXTS.has(s.display), 'error display is a known message ' + where());
  if (s.justEvaluated && s.mode !== 'fractions') assert.equal(s.display, C.formatNumber(s.result), where());
  assert.ok(s.fracCursor.term >= 0 && s.fracCursor.term < s.fracTerms.length, where());
  assert.ok(['whole', 'num', 'den'].includes(s.fracCursor.slot));
  for (const t of s.fracTerms) {
    for (const f of ['whole', 'num', 'den']) assert.equal(typeof t[f], 'string');
    assert.ok(['', '+', '-', '*', '/'].includes(t.op));
  }
}

test('random key sequences never throw and keep every invariant', () => {
  fc.assert(fc.property(fc.array(action, { maxLength: 60 }), seq => {
    const calc = newCalc();
    for (let i = 0; i < seq.length; i++) {
      apply(calc, seq[i]);
      checkInvariants(calc.state, seq.slice(0, i + 1));
    }
  }), { numRuns: 3000 });
});

test('typed "a op b =" matches the parser', () => {
  const ops = { '+': '+', '−': '-', '×': '*', '÷': '/' };
  fc.assert(fc.property(fc.nat(99999), fc.constantFrom(...Object.keys(ops)), fc.nat(99999), (a, op, b) => {
    const s = press(newCalc(), [...String(a), op, ...String(b), '=']);
    if (op === '÷' && b === 0) { assert.equal(s.display, "Can't divide by 0"); return; }
    assert.equal(s.display, C.formatNumber(C.evaluate(`${a}${ops[op]}${b}`)));
    assert.equal(s.calcHistory[0].result, s.result);
  }), { numRuns: 2000 });
});

test('Apple-style percent', () => {
  fc.assert(fc.property(fc.nat(9999), fc.nat(999), fc.constantFrom('+', '−', '×', '÷'), (a, b, op) => {
    const s = press(newCalc(), [...String(a), op, ...String(b), '%', '=']);
    const expected = { '+': a + a * b / 100, '−': a - a * b / 100, '×': a * (b / 100), '÷': a / (b / 100) }[op];
    if (op === '÷' && b === 0) return; // division by zero, covered elsewhere
    assert.ok(Math.abs(s.result - expected) <= 1e-9 * Math.max(1, Math.abs(expected)), `${a}${op}${b}% → ${s.display}`);
  }), { numRuns: 1000 });
});

test('AC always returns to a clean standard state', () => {
  fc.assert(fc.property(fc.array(action, { maxLength: 30 }), seq => {
    const calc = newCalc();
    for (const a of seq) apply(calc, a);
    calc.setMode('standard');
    calc.pressKey('AC');
    const s = calc.state;
    assert.deepEqual([s.expression, s.display, s.result, s.justEvaluated, s.hasError], ['', '0', null, false, false]);
  }), { numRuns: 1000 });
});

test('fractions: p/q ∘ r/s is exact', () => {
  const opKeys = { '+': (a, b) => a + b, '−': (a, b) => a - b, '×': (a, b) => a * b };
  fc.assert(fc.property(fc.integer({ min: 1, max: 50 }), fc.integer({ min: 1, max: 50 }),
    fc.integer({ min: 1, max: 50 }), fc.integer({ min: 1, max: 50 }), fc.constantFrom(...Object.keys(opKeys)),
    (p, q, r, s, op) => {
      const calc = newCalc();
      calc.setMode('fraction');
      const st = press(calc, ['a/b', ...String(p), 'a/b', ...String(q), op, 'a/b', ...String(r), 'a/b', ...String(s), '=']);
      // exact rational: (p*s ∘ r*q) / (q*s)
      const num = op === '×' ? p * r : opKeys[op](p * s, r * q);
      const den = op === '×' ? q * s : q * s;
      const g = C.gcd(num, den);
      assert.deepEqual({ n: st.fracResultFrac.n + 0, d: st.fracResultFrac.d }, { n: num / g + 0, d: den / g });
    }), { numRuns: 1000 });
});

const SCENARIOS = [
  // [mode, keys, expected display]
  ['standard', '2 + 3 × 4 =', '14'],
  ['standard', '1 0 0 + 1 0 % =', '110'],
  ['standard', '1 0 0 − 1 0 % =', '90'],
  ['standard', '1 0 0 × 1 0 % =', '10'],
  ['standard', '5 0 % =', '0.5'],
  ['standard', '1 ÷ 0 =', "Can't divide by 0"],
  ['standard', '1 ÷ 0 = 7 =', '7'],
  ['standard', '0 . 1 + 0 . 2 =', '0.3'],
  ['standard', '. 5 + . 5 =', '1'],
  ['standard', '1 . . 5 =', '1.5'],
  ['standard', '2 + 3 = × 2 =', '10'],
  ['standard', '2 + 3 = +/- =', '-5'],
  ['standard', '+/- 3 + 5 =', '2'],
  ['standard', '− 3 + 5 =', '2'],
  ['standard', '9 ⌫ 4 =', '4'],
  ['standard', '2 + 3 = ⌫', '0'],
  ['scientific', '√ 1 6 =', '4'],
  ['scientific', '2 x² =', '4'],
  ['scientific', '2 xʸ 1 0 =', '1024'],
  ['scientific', '− 2 xʸ 2 =', '-4'],
  ['scientific', '2 7 ʸ√x 3 =', '3'],
  ['scientific', '5 n! =', '120'],
  ['scientific', 'sin 3 0 =', '0.5'],
  ['scientific', '( 1 + 2 ) ( 3 + 4 ) =', '21'],
  ['scientific', '( ( 1 + 2 =', '3'],
  ['scientific', '2 π =', C.formatNumber(2 * Math.PI)],
  ['standard', '+/- +/- 3 =', '3'],
  ['standard', '3 +/- +/- =', '3'],
  ['standard', '5 × +/- 3 =', '-15'],
  ['standard', '5 − 3 +/- =', '8'],
  ['scientific', '1 EE 3 =', '1000'],
  // EE после "=" продолжает результат, а не приклеивает E к устаревшему экрану.
  ['scientific', '5 = EE 3 =', '5000'],
  ['scientific', '2 + 3 = EE 2 =', '500'],
  ['scientific', '0 = EE', '0E'],
  ['scientific', '0 = EE 2 =', '0'],
  ['scientific', '1 6 = √', '4'],
  ['scientific', '4 = 1/x', '0.25'],
  ['scientific', '1 a⁄b 4 =', '0.25'],
];
for (const [mode, keys, expected] of SCENARIOS) {
  test(`${mode}: ${keys} → ${expected}`, () => {
    const calc = newCalc();
    calc.setMode(mode);
    assert.equal(press(calc, keys.split(' ')).display, expected);
  });
}

test('2nd flips keys to their inverses and angle mode cycles DEG → RAD → GRAD', () => {
  const calc = newCalc();
  calc.setMode('scientific');
  calc.pressKey('2nd');
  assert.equal(calc.state.altSet, true);
  assert.equal(press(calc, ['asin', '1', '=']).display, '90');
  calc.pressKey('Rad');
  assert.equal(calc.state.angleMode, 'rad');
  calc.pressKey('Rad');
  assert.equal(calc.state.angleMode, 'grad');
  assert.equal(press(calc, ['AC', 'asin', '1', '=']).display, '100');
});

test('fractions: typing, simplifying and error cases', () => {
  const calc = newCalc();
  calc.setMode('fraction');
  press(calc, ['a/b', '6', 'a/b', '4', 'Simp']);
  assert.deepEqual(calc.state.fracTerms[0], { whole: '1', num: '1', den: '2', op: '' });

  calc.pressKey('AC');
  press(calc, ['2', 'a/b', '1', 'a/b', '2', '×', '2', '=']);
  assert.equal(calc.state.fracResult, 5);

  calc.pressKey('AC');
  press(calc, ['a/b', '1', '=']);
  assert.equal(calc.state.hasError, true);
  assert.equal(calc.state.display, 'Incomplete fraction');

  calc.pressKey('AC');
  press(calc, ['a/b', '1', 'a/b', '0', '=']);
  assert.equal(calc.state.display, 'Division by zero');
});

test('memory keys and recall', () => {
  const calc = newCalc();
  press(calc, ['7', '=']);
  calc.memStore();
  calc.memAdd();
  assert.equal(calc.state.memory, 14);
  calc.pressKey('AC');
  press(calc, ['2', '×']);
  calc.memRecall();
  assert.equal(press(calc, ['=']).display, '28');
  calc.memClear();
  assert.equal(calc.state.memory, 0);
});

test('history and memory persist through storage; corrupted storage is ignored', () => {
  const storage = memoryStorage();
  const calc = newCalc(storage);
  press(calc, ['2', '+', '2', '=']);
  calc.memStore();

  const again = newCalc(storage);
  assert.equal(again.state.calcHistory.length, 1);
  assert.equal(again.state.calcHistory[0].result, 4);
  assert.equal(again.state.memory, 4);
  again.useHistoryEntry(0);
  assert.equal(again.state.expression, '4');

  for (const bad of ['{not json', '{"a":1}', '42', 'null', '[1,null,"x"]']) {
    const c = newCalc(memoryStorage({ [C.HISTORY_KEY]: bad }));
    assert.ok(Array.isArray(c.state.calcHistory));
    press(c, ['1', '+', '1', '=']);
    assert.equal(c.state.display, '2');
  }
});

test('history keeps the newest 50 entries', () => {
  const calc = newCalc();
  for (let i = 1; i <= 60; i++) press(calc, [...String(i), '=']);
  assert.equal(calc.state.calcHistory.length, 50);
  assert.equal(calc.state.calcHistory[0].result, 60);
  calc.clearHistory();
  assert.equal(calc.state.calcHistory.length, 0);
});

test('listeners fire for render, evaluation and history', () => {
  const seen = [];
  const calc = C.createCalculator({ on: { change: () => seen.push('change'), evaluated: () => seen.push('evaluated'), history: () => seen.push('history') } });
  press(calc, ['1', '+', '1', '=']);
  assert.ok(seen.includes('change') && seen.includes('evaluated') && seen.includes('history'));
});
