// calc-engine.js: the immediate-execution iOS-style calculator.
const test = require('node:test');
const assert = require('node:assert/strict');
const fc = require('fast-check');
const E = require('../src/calc-engine.js');

const NBSP = ' ';
const newEngine = (opts = {}) => E.createEngine({ now: () => 0, random: () => 0.5, ...opts });

// Keys as the page sends them: digits/"." → inputNum, + - * / = → inputOp, …
function press(engine, keys) {
  for (const k of keys) {
    if (/^[0-9.]$/.test(k)) engine.inputNum(k);
    else if (['+', '-', '*', '/', '='].includes(k)) engine.inputOp(k);
    else if (k === 'C') engine.pressClear();
    else if (k === '⌫') engine.backspace();
    else if (k === '%') engine.applyPercent();
    else if (k === '±') engine.negate();
    else engine.sci(k);
  }
  return engine.state;
}
const typed = (engine, text) => press(engine, text.split(' ').flatMap(t => (/^\d/.test(t) ? [...t] : [t])));

// ---------------------------------------------------------------- regression suite (from test-logic.js)
const CASES = [
  ['5000 + 50 % =', '7' + NBSP + '500'],
  ['1234 m+ C 1000 + mr =', '2' + NBSP + '234'],
  ['2000 + 100 % =', '4' + NBSP + '000'],
  ['500 + 700 + 800 =', '2' + NBSP + '000'],
  ['5 + 3 C 7 =', '12'],
  ['5 + ± ⌫ 3 =', '8'],
  ['5 + 3 C .', '5 + 0,'],
  ['5 + ± ⌫ .', '5 + 0,'],
  ['2 + 3 =', '5'],
  ['2 + 3 * 4 =', '20'],
  ['100 + 20 % =', '120'],
  ['1 + 2 = + 3 =', '6'],
  ['5 + - 3 =', '2'],
  ['5 + ± 3 =', '2'],
  ['5 / 0 =', E.ERROR],
  ['2500 sqrt', '50'],
  ['5 + 3 C', '5 + 0'],
  ['5 + 3 C C', '0'],
  ['0 recip', E.ERROR],
  ['0 recip +', '0'],
  ['. 5 + . 5 =', '1'],
  ['1 . . 5 =', '1,5'],
  ['3 square', '9'],
  ['2 cube', '8'],
  ['2 pow 10 =', '1' + NBSP + '024'],
  ['5 fact', '120'],
  ['100 log10', '2'],
  ['9 ± =', '-9'],
  ['7 ⌫', '0'],
  ['12 ⌫', '1'],
  ['2 + 3 = ⌫', '0'],
  // fixed in 1.1: division by zero in the middle of a chain
  ['5 / 0 +', E.ERROR],
  ['5 / 0 + 3 =', '3'],
  // fixed in 1.1: exponent-form numbers are read back correctly
  ['0.0000001 m+ C C 5 + mr =', E.formatNumber(5.0000001)],
  ['99999999 * 99999999 * 99999999 = + 1 =', E.formatNumber(99999999 * 99999999 * 99999999 + 1)],
];
for (const [keys, expected] of CASES) {
  test(`${keys} → ${expected}`, () => {
    assert.equal(typed(newEngine(), keys).currentExpr, expected);
  });
}

test('Rand adds a history entry; errors are never stored', () => {
  const engine = newEngine();
  engine.sci('rand');
  assert.equal(engine.state.history.length, 1);
  assert.equal(engine.state.history[0].expr, 'Rand');
  assert.equal(engine.state.currentExpr, '0,5');
  typed(engine, '1 / 0 =');
  assert.equal(engine.state.history.length, 1);
});

test('2nd switches trig to inverse, Rad switches angle unit', () => {
  const engine = newEngine();
  engine.sci('2nd');
  assert.equal(typed(engine, '1 sin').currentExpr, '90');
  engine.sci('rad');
  assert.equal(engine.state.isRad, true);
  assert.equal(typed(engine, 'C C 1 sin').currentExpr, E.formatNumber(Math.PI / 2));
});

test('clear label and active operator', () => {
  const engine = newEngine();
  assert.equal(engine.clearLabel(), 'AC');
  typed(engine, '5 +');
  assert.equal(engine.clearLabel(), 'C');
  assert.equal(engine.activeOperator(), '+');
  typed(engine, '3');
  assert.equal(engine.activeOperator(), null);
});

// ---------------------------------------------------------------- properties
const ACTIONS = ['+', '-', '*', '/', '=', 'C', '⌫', '%', '±', ...[
  'sinh', 'cosh', 'mc', 'm+', 'm-', 'mr', '2nd', 'square', 'cube', 'pow', 'exp', 'ten-pow', 'recip',
  'sqrt', 'cbrt', 'ln', 'log10', 'fact', 'sin', 'cos', 'tan', 'rad', 'pi', 'e', 'rand']];
const DIGITS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'];
const key = fc.oneof(fc.constantFrom(...DIGITS), fc.constantFrom(...DIGITS), fc.constantFrom(...ACTIONS));

function checkInvariants(engine, trail) {
  const s = engine.state;
  const where = () => `after ${JSON.stringify(trail)}: ${JSON.stringify([s.currentExpr, s.lastExpression])}`;
  for (const text of [s.currentExpr, s.lastExpression]) {
    assert.equal(typeof text, 'string', where());
    assert.ok(!/NaN|undefined|Infinity/.test(text), where());
  }
  assert.ok(s.currentExpr === E.ERROR || !s.currentExpr.includes(E.ERROR), 'Ошибка stands alone ' + where());
  assert.ok(s.currentExpr.length > 0, where());
  assert.ok(['C', 'AC'].includes(engine.clearLabel()));
  assert.ok(Number.isFinite(s.memory), where());
  assert.ok(s.history.length <= E.HISTORY_LIMIT);
  if (s.firstOperand !== null) assert.ok(Number.isFinite(s.firstOperand), where());
  if (s.isResultShown && s.currentExpr !== E.ERROR) {
    assert.ok(Number.isFinite(E.parseDisplay(s.currentExpr)), 'a shown result is a number ' + where());
  }
  const last = engine.getLastNumber();
  if (last) assert.ok(Number.isFinite(E.parseDisplay(last)), 'last number parses ' + where());
}

test('random key sequences never throw and keep every invariant', () => {
  fc.assert(fc.property(fc.array(key, { maxLength: 60 }), seq => {
    const engine = newEngine();
    for (let i = 0; i < seq.length; i++) {
      press(engine, [seq[i]]);
      checkInvariants(engine, seq.slice(0, i + 1));
    }
  }), { numRuns: 3000 });
});

test('a chain of operations is folded left to right (no precedence), like an iPhone', () => {
  const ops = { '+': (a, b) => a + b, '-': (a, b) => a - b, '*': (a, b) => a * b };
  const step = fc.tuple(fc.constantFrom('+', '-', '*'), fc.nat(9999));
  fc.assert(fc.property(fc.nat(9999), fc.array(step, { minLength: 1, maxLength: 6 }), (first, steps) => {
    const engine = newEngine();
    typed(engine, String(first));
    let expected = first;
    for (const [op, n] of steps) {
      press(engine, [op, ...String(n)]);
      expected = ops[op](expected, n);
    }
    press(engine, ['=']);
    assert.equal(engine.state.currentExpr, E.formatNumber(expected));
  }), { numRuns: 2000 });
});

test('formatNumber and parseDisplay round-trip', () => {
  fc.assert(fc.property(fc.integer({ min: -1e15, max: 1e15 }), n => {
    assert.equal(E.parseDisplay(E.formatNumber(n)), n);
  }), { numRuns: 2000 });
  fc.assert(fc.property(fc.double({ min: -1e300, max: 1e300, noNaN: true }), x => {
    const back = E.parseDisplay(E.formatNumber(x));
    assert.ok(Math.abs(back - x) <= 1e-6 * Math.max(1, Math.abs(x)), `${x} → ${E.formatNumber(x)} → ${back}`);
  }), { numRuns: 2000 });
});

test('every formatted number is recognised as "the last number"', () => {
  fc.assert(fc.property(fc.double({ min: -1e300, max: 1e300, noNaN: true }), fc.constantFrom('', '5 + ', '12 × '), (x, prefix) => {
    const engine = newEngine();
    engine.state.currentExpr = prefix + E.formatNumber(x);
    assert.equal(engine.getLastNumber(), E.formatNumber(x));
  }), { numRuns: 2000 });
});

test('memory: m+ then m- of the same number is a no-op', () => {
  fc.assert(fc.property(fc.nat(999999), fc.nat(999999), (m, x) => {
    const engine = newEngine();
    typed(engine, `${m} m+ C C ${x} m+ m-`);
    assert.equal(engine.state.memory, m);
  }), { numRuns: 1000 });
});

test('history is capped, persisted and reloaded; bad storage is ignored', () => {
  const data = {};
  const storage = { getItem: k => data[k] ?? null, setItem: (k, v) => { data[k] = v; } };
  const engine = newEngine({ storage });
  for (let i = 0; i < E.HISTORY_LIMIT + 10; i++) typed(engine, `${i} + 1 =`);
  assert.equal(engine.state.history.length, E.HISTORY_LIMIT);

  const again = newEngine({ storage });
  assert.equal(again.state.history.length, E.HISTORY_LIMIT);
  again.useHistoryEntry(again.state.history.length - 1);
  assert.equal(again.state.currentExpr, E.formatNumber(E.HISTORY_LIMIT + 10));

  for (const bad of ['{', '"x"', '{"a":1}', '[null, 5]']) {
    const e = newEngine({ storage: { getItem: () => bad, setItem() {} } });
    assert.ok(Array.isArray(e.state.history));
    assert.equal(typed(e, '1 + 1 =').currentExpr, '2');
  }
});

test('onChange fires on every visible change', () => {
  let calls = 0;
  const engine = E.createEngine({ onChange: () => calls++ });
  typed(engine, '1 + 2 =');
  assert.ok(calls >= 4);
});
