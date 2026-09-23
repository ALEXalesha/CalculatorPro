// Properties of the pure parts of calc-core.js: parser, number and fraction formatting.
const test = require('node:test');
const assert = require('node:assert/strict');
const fc = require('fast-check');
const C = require('../src/calc-core.js');

const RUNS = { numRuns: 2000 };
const ERRORS = new Set(['EMPTY', 'SYNTAX', 'MISMATCH', 'DIV_ZERO', 'DOMAIN', 'OVERFLOW', 'UNKNOWN_FN']);

// ---------------------------------------------------------------- expression trees
const FUNCS = ['sqrt', 'abs', 'sin', 'cos', 'ln', 'log', 'exp', 'cbrt'];
const number = fc.oneof(
  fc.integer({ min: 0, max: 1000 }),
  fc.integer({ min: 0, max: 100000 }).map(n => n / 100),
);
const { tree } = fc.letrec(tie => ({
  tree: fc.oneof({ maxDepth: 5, depthSize: 'small', withCrossShrink: true },
    tie('leaf'), tie('bin'), tie('bin'), tie('neg'), tie('fn')),
  leaf: number.map(v => ({ t: 'num', v })),
  bin: fc.record({ t: fc.constant('bin'), op: fc.constantFrom('+', '-', '*', '/', '^'), l: tie('tree'), r: tie('tree') }),
  neg: fc.record({ t: fc.constant('neg'), x: tie('tree') }),
  fn: fc.record({ t: fc.constant('fn'), f: fc.constantFrom(...FUNCS), x: tie('tree') }),
}));

function full(n) {
  switch (n.t) {
    case 'num': return String(n.v);
    case 'neg': return `(-${full(n.x)})`;
    case 'fn': return `${n.f}(${full(n.x)})`;
    default: return `(${full(n.l)}${n.op}${full(n.r)})`;
  }
}

// Minimal parentheses for the grammar: + - (L, 10) < * / (L, 20) < unary - (30) < ^ (R, 40).
// Text = printed form; top = binding of the top-level infix operator (∞ for atoms and
// prefix forms); trail = lowest binding still open on the right edge.
const INF = Infinity;
function minimal(n) {
  const paren = p => ({ text: `(${p.text})`, top: INF, trail: INF });
  switch (n.t) {
    case 'num': return { text: String(n.v), top: INF, trail: INF };
    case 'fn': return { text: `${n.f}(${minimal(n.x).text})`, top: INF, trail: INF };
    case 'neg': {
      let x = minimal(n.x);
      if (x.top <= 30) x = paren(x);
      return { text: '-' + x.text, top: INF, trail: Math.min(30, x.trail) };
    }
    default: {
      const [lbp, rbp] = { '+': [10, 10], '-': [10, 10], '*': [20, 20], '/': [20, 20], '^': [40, 39] }[n.op];
      let l = minimal(n.l);
      if (l.trail < lbp) l = paren(l);
      let r = minimal(n.r);
      if (r.top <= rbp) r = paren(r);
      return { text: l.text + n.op + r.text, top: lbp, trail: Math.min(rbp, r.trail) };
    }
  }
}

// Direct tree evaluation with the same primitive operations as the engine.
function reference(n, mode) {
  switch (n.t) {
    case 'num': return n.v;
    case 'neg': return -reference(n.x, mode);
    case 'fn': return C.applyFunc(n.f, reference(n.x, mode), mode);
    default: {
      const a = reference(n.l, mode), b = reference(n.r, mode);
      return C.OPERATORS[n.op].fn(a, b);
    }
  }
}

function outcome(f) {
  try {
    const r = f();
    return Number.isFinite(r) ? r : 'OVERFLOW';
  } catch (e) {
    return e.message;
  }
}

test('fully parenthesised text evaluates like the tree', () => {
  fc.assert(fc.property(tree, fc.constantFrom('deg', 'rad', 'grad'), (t, mode) => {
    assert.equal(outcome(() => C.evaluate(full(t), mode)), outcome(() => reference(t, mode)));
  }), RUNS);
});

test('minimally parenthesised text evaluates like the tree (pins precedence + associativity)', () => {
  fc.assert(fc.property(tree, t => {
    assert.equal(outcome(() => C.evaluate(minimal(t).text)), outcome(() => reference(t, 'deg')));
  }), { numRuns: 4000 });
});

test('whitespace between tokens is ignored', () => {
  fc.assert(fc.property(tree, t => {
    const text = minimal(t).text;
    const spaced = text.replace(/([+\-*/^()])/g, ' $1 ');
    assert.equal(outcome(() => C.evaluate(spaced)), outcome(() => C.evaluate(text)));
  }), RUNS);
});

test('unicode × ÷ − are accepted after expressionToInternal', () => {
  fc.assert(fc.property(tree, t => {
    const text = minimal(t).text;
    const pretty = text.replace(/\*/g, '×').replace(/\//g, '÷').replace(/-/g, '−');
    assert.equal(outcome(() => C.evaluate(C.expressionToInternal(pretty))), outcome(() => C.evaluate(text)));
  }), RUNS);
});

test('random garbage only ever produces calculator errors', () => {
  const piece = fc.constantFrom('1', '2', '0', '.', '..', 'e', 'E', 'π', '+', '-', '*', '/', '^', '%', '(', ')', ' ',
    'sin', 'cos', 'tan', 'cot', 'asin', 'sqrt', 'cbrt', 'ln', 'log', 'fact', 'exp10', 'exp', 'abs', 'inv', 'neg',
    'x', '#', 'sinh', '1e308', '9'.repeat(400));
  fc.assert(fc.property(fc.array(piece, { maxLength: 30 }), parts => {
    const text = parts.join('');
    try {
      const r = C.evaluate(text);
      assert.ok(Number.isFinite(r), `finite result for ${text}`);
    } catch (e) {
      assert.ok(ERRORS.has(e.message), `unexpected error "${e.message}" for ${JSON.stringify(text)}`);
    }
  }), { numRuns: 5000 });
});

test('non-negative numbers survive tokenise unchanged', () => {
  fc.assert(fc.property(fc.double({ min: 0, max: 1e300, noNaN: true }), x => {
    const tokens = C.tokenise(String(x));
    assert.deepEqual(tokens, [{ type: 'num', val: x }]);
  }), RUNS);
});

test('formatNumber output is always valid input and keeps 12 significant digits', () => {
  fc.assert(fc.property(fc.double({ min: -1e300, max: 1e300, noNaN: true }), x => {
    const s = C.formatNumber(x);
    assert.ok(!/NaN|undefined|Infinity/.test(s), s);
    const back = C.evaluate(s);
    const exp = Math.abs(x) >= 1e15 || (Math.abs(x) < 1e-10 && x !== 0);
    const tolerance = exp ? 1e-6 : 1e-11;
    assert.ok(Math.abs(back - x) <= tolerance * Math.max(1, Math.abs(x)), `${x} → ${s} → ${back}`);
  }), RUNS);
});

test('formatNumber of non-finite values says Overflow', () => {
  for (const v of [Infinity, -Infinity, NaN, 'abc']) assert.equal(C.formatNumber(v), 'Overflow');
});

test('decimalToFraction recovers any p/q with a small denominator, fully reduced', () => {
  fc.assert(fc.property(fc.integer({ min: -5000, max: 5000 }), fc.integer({ min: 1, max: 1000 }), (p, q) => {
    const f = C.decimalToFraction(p / q);
    const g = C.gcd(p, q);
    assert.deepEqual({ n: f.n + 0, d: f.d }, { n: p / g + 0, d: q / g });
    assert.equal(C.gcd(f.n, f.d), 1);
    assert.ok(f.d > 0);
  }), RUNS);
});

test('formatFraction text reads back as the same value', () => {
  fc.assert(fc.property(fc.integer({ min: -5000, max: 5000 }), fc.integer({ min: 2, max: 500 }), (p, q) => {
    const text = C.formatFraction(p / q);
    if (p % q === 0) { assert.equal(text, null); return; }
    const m = text.match(/^(-?)(?:(\d+) )?(\d+)\/(\d+)$/);
    assert.ok(m, text);
    const value = (m[1] ? -1 : 1) * ((+m[2] || 0) + (+m[3]) / (+m[4]));
    assert.ok(Math.abs(value - p / q) < 1e-9, `${p}/${q} → ${text}`);
    assert.ok(+m[3] < +m[4], 'proper fractional part');
  }), RUNS);
});

test('trigonometric identities hold in every angle mode', () => {
  fc.assert(fc.property(fc.double({ min: -1e4, max: 1e4, noNaN: true }), fc.constantFrom('deg', 'rad', 'grad'), (x, mode) => {
    const s = C.applyFunc('sin', x, mode), c = C.applyFunc('cos', x, mode);
    assert.ok(Math.abs(s * s + c * c - 1) < 1e-12);
  }), RUNS);
  fc.assert(fc.property(fc.double({ min: -1, max: 1, noNaN: true }), fc.constantFrom('deg', 'rad', 'grad'), (x, mode) => {
    assert.ok(Math.abs(C.applyFunc('sin', C.applyFunc('asin', x, mode), mode) - x) < 1e-12);
  }), RUNS);
});

test('termToExpression sign conventions', () => {
  const t = (whole, num, den) => C.termToExpression({ whole, num, den, op: '' });
  assert.equal(t('', '', ''), '0');
  assert.equal(t('5', '', ''), '5');
  assert.equal(t('5', '1', '2'), '(5+1/2)');
  assert.equal(t('-5', '1', '2'), '-(5+1/2)');
  assert.equal(t('5', '-1', '2'), '-(5+1/2)');
  assert.equal(t('-5', '-1', '2'), '(5+1/2)');
  assert.equal(t('', '3', '4'), '(3/4)');
  assert.equal(t('', '3', ''), '3');
});

const EXAMPLES = [
  ['2+3*4', 14], ['(2+3)*4', 20], ['10-4-3', 3], ['100/10/5', 2],
  ['2^10', 1024], ['2^3^2', 512], ['-2^2', -4], ['(-2)^2', 4], ['2^-2', 0.25], ['2^-2^2', 0.0625],
  ['-2*3', -6], ['3*-2', -6], ['--3', 3], ['+5', 5], ['-(2+3)', -5],
  ['(1+2)(3+4)', 21], ['2(3)', 6], ['1/(4)', 0.25], ['2e3', 2000], ['1.5E-3', 0.0015],
  ['sqrt(16)', 4], ['cbrt(27)', 3], ['abs(-7)', 7], ['fact(5)', 120], ['exp10(3)', 1000],
  ['log(1000)', 3], ['ln(1)', 0], ['sin(30)', 0.5], ['cos(60)', 0.5], ['tan(45)', 1],
  ['neg(5)', -5], ['inv(4)', 0.25], ['7%3', 1], ['2*π', 2 * Math.PI], ['e', Math.E],
];
for (const [expr, expected] of EXAMPLES) {
  test(`evaluate ${expr} = ${expected}`, () => {
    assert.ok(Math.abs(C.evaluate(expr) - expected) < 1e-12, String(C.evaluate(expr)));
  });
}

const ERROR_EXAMPLES = [
  ['', 'EMPTY'], ['   ', 'EMPTY'], ['1/0', 'DIV_ZERO'], ['inv(0)', 'DIV_ZERO'], ['sqrt(-1)', 'DOMAIN'],
  ['ln(0)', 'DOMAIN'], ['asin(2)', 'DOMAIN'], ['tan(90)', 'DOMAIN'], ['cot(0)', 'DOMAIN'], ['fact(2.5)', 'DOMAIN'],
  ['fact(171)', 'OVERFLOW'], ['10^400', 'OVERFLOW'], ['(1+2', 'MISMATCH'], ['1+2)', 'MISMATCH'],
  ['1+', 'SYNTAX'], ['*', 'SYNTAX'], ['1..2', 'SYNTAX'], ['#', 'SYNTAX'], ['()', 'SYNTAX'],
];
for (const [expr, error] of ERROR_EXAMPLES) {
  test(`evaluate ${JSON.stringify(expr)} throws ${error}`, () => {
    assert.throws(() => C.evaluate(expr), { message: error });
  });
}

// ---------------------------------------------------------------- fraction text
// Нашлось на кадре для README: строка над результатом в режиме Fraction
// показывала «1/2 + 1/3» как «12 + 13». Текст собирался вырезанием тегов из
// дроби «в столбик», и черта между числителем и знаменателем пропадала - та же
// строка уходила и в панель истории.
const cursorAt = { term: 0, slot: 'whole' };
const term = (whole, num, den, op = null) => ({ whole, num, den, op });

test('fraction text keeps the fraction bar', () => {
  assert.equal(C.fractionExprText([term('', '1', '2', '+'), term('', '1', '3')], cursorAt), '1/2 + 1/3');
});

test('fraction text shows mixed numbers and every operator', () => {
  const terms = [term('2', '1', '2', '*'), term('', '3', '4', '/'), term('5', '', '', '-'), term('', '7', '8')];
  assert.equal(C.fractionExprText(terms, cursorAt), '2 1/2 × 3/4 ÷ 5 − 7/8');
});

test('fraction text: one bar per fraction term, digits never glued across it', () => {
  const digits = fc.stringMatching(/^[1-9][0-9]{0,2}$/);
  const anyTerm = fc.record({
    whole: fc.oneof(fc.constant(''), digits),
    frac: fc.boolean(),
    num: digits, den: digits,
    op: fc.constantFrom('+', '-', '*', '/'),
  });
  fc.assert(fc.property(fc.array(anyTerm, { minLength: 1, maxLength: 5 }), (raw) => {
    const terms = raw.map((t, i) => {
      const withFrac = t.frac || !t.whole; // терм из одного целого - без дроби
      return term(t.whole, withFrac ? t.num : '', withFrac ? t.den : '',
        i < raw.length - 1 ? t.op : null);
    });
    const text = C.fractionExprText(terms, cursorAt);
    const fractions = terms.filter((t) => t.num !== '' || t.den !== '').length;
    assert.equal((text.match(/\//g) || []).length, fractions, text);
    for (const t of terms) {
      if (t.num !== '' && t.den !== '') assert.ok(text.includes(`${t.num}/${t.den}`), `${text} без ${t.num}/${t.den}`);
    }
  }), RUNS);
});
