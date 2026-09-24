// Режим «Программист» (1.6.0). Примеры - из test-vectors/programmer.json: тот же файл
// проверяет и Calc Pro на C#, так что при одних нажатиях обе версии показывают одно.
// Ожидаемое в файле посчитано отдельно (Python, маска 2^64), а не этим кодом.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const fc = require('fast-check');
const P = require('../src/programmer-core.js');

const VECTORS = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '..', 'test-vectors', 'programmer.json'), 'utf8'));
const BASES = [2, 8, 10, 16];

test('there are enough shared examples', () => {
  assert.ok(VECTORS.length >= 40);
});

for (const v of VECTORS) {
  test('shared example: ' + v.name, () => {
    const calc = P.createProgrammer();
    for (const k of v.keys) calc.press(k);
    assert.equal(calc.state.error, v.error, 'ошибка');
    if (v.dec !== undefined) {
      const all = calc.all();
      assert.deepEqual({ hex: all.HEX, dec: all.DEC, oct: all.OCT, bin: all.BIN },
        { hex: v.hex, dec: v.dec, oct: v.oct, bin: v.bin });
    }
    if (v.display !== undefined) assert.equal(calc.display(), v.display);
    if (v.expression !== undefined) assert.equal(calc.expression(), v.expression);
  });
}

const int64 = fc.bigIntN(64);

test('every number reads back from every base, grouped or not', () => {
  fc.assert(fc.property(int64, (v) => BASES.every((b) =>
    P.parse(P.format(v, b), b) === v && P.parse(P.group(P.format(v, b), b), b) === v)), { numRuns: 3000 });
});

test('typing a number digit by digit gives that number', () => {
  fc.assert(fc.property(fc.bigUintN(63), (v) => BASES.every((b) => {
    const calc = P.createProgrammer();
    calc.setBase(b);
    for (const c of P.format(v, b)) calc.press(c);
    return calc.state.value === v;
  })), { numRuns: 1000 });
});

test('arithmetic wraps like 64-bit integers', () => {
  const wrap = (x) => BigInt.asIntN(64, x);
  fc.assert(fc.property(int64, int64, (a, b) => {
    const ok = P.apply('+', a, b) === wrap(a + b) && P.apply('−', a, b) === wrap(a - b)
      && P.apply('×', a, b) === wrap(a * b)
      && P.apply('AND', a, b) === (a & b) && P.apply('OR', a, b) === (a | b) && P.apply('XOR', a, b) === (a ^ b);
    if (b === 0n) return ok && P.apply('÷', a, b) === null && P.apply('Mod', a, b) === null;
    return ok && P.apply('÷', a, b) === wrap(a / b) && P.apply('Mod', a, b) === wrap(a % b);
  }), { numRuns: 3000 });
});

test('shifts use the low six bits of the count', () => {
  fc.assert(fc.property(int64, fc.integer({ min: -1000, max: 1000 }), (a, n) => {
    const s = BigInt(n & 63);
    return P.apply('<<', a, BigInt(n)) === BigInt.asIntN(64, a << s) && P.apply('>>', a, BigInt(n)) === a >> s;
  }), { numRuns: 2000 });
});

test('switching bases never changes the value', () => {
  fc.assert(fc.property(int64, fc.constantFrom(...BASES), fc.constantFrom(...BASES), (v, b1, b2) => {
    const calc = P.createProgrammer();
    calc.setBase(16);
    for (const c of P.format(v, 16)) calc.press(c);
    calc.setBase(b1);
    calc.setBase(b2);
    return calc.state.value === v;
  }), { numRuns: 1000 });
});

test('not a number in that base', () => {
  for (const [text, b] of [['12', 2], ['8', 8], ['G', 16], ['', 10], ['-', 10], ['-5', 16],
    ['9223372036854775808', 10], ['10000000000000000', 16]]) {
    assert.equal(P.parse(text, b), null, text + ' в ' + b);
  }
  assert.equal(P.parse('-9223372036854775808', 10), -(1n << 63n));
});

test('the same operators as the C# version, with the same precedence', () => {
  const src = fs.readFileSync(path.join(__dirname, '..', '..', 'calcpro-wpf', 'src', 'CalcPro.Core', 'Services', 'ProgrammerCalculator.cs'), 'utf8');
  const block = src.match(/Precedence = new\(\)\s*\{([\s\S]*?)\};/)[1];
  const cs = {};
  for (const m of block.matchAll(/\["([^"]+)"\] = (\d+)/g)) cs[m[1]] = Number(m[2]);
  assert.deepEqual(cs, P.PRECEDENCE);
});

test('the keypad has every digit and every operator', () => {
  const keys = new Set(P.PROG_LAYOUT.map((b) => b.k));
  for (const k of [...'0123456789ABCDEF', ...Object.keys(P.PRECEDENCE), 'NOT', '±', '(', ')', '=', 'AC', 'CE', '⌫']) {
    assert.ok(keys.has(k), 'нет клавиши ' + k);
  }
  // Пять столбцов: «=» занимает два места, всего 7 рядов по 5.
  const cells = P.PROG_LAYOUT.reduce((n, b) => n + (b.span || 1), 0);
  assert.equal(cells, 35);
});
