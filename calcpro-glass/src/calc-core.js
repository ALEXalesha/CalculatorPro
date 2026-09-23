/*
 * Calc Pro Glass — calculator engine.
 *
 * Pure logic, no DOM: expression parser (shunting-yard, no eval), number and
 * fraction formatting, and the key-press state machine for the Standard,
 * Scientific and Fraction modes. The page (app.js) renders `calc.state` and
 * forwards clicks/keys to `calc.pressKey()`. Node tests require() this file.
 *
 * UMD: exposes `CalcCore` on the page, `module.exports` under Node.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.CalcCore = factory();
}(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  // ===================================================================
  // LAYOUTS (data only; app.js turns them into buttons)
  // type: digit | op | util | del | eq | sci | frac
  // ===================================================================
  const STD_LAYOUT = [
    { k: '⌫', t: 'del' }, { k: 'AC', t: 'util' }, { k: '%', t: 'util' }, { k: '÷', t: 'op' },
    { k: '7', t: 'digit' }, { k: '8', t: 'digit' }, { k: '9', t: 'digit' }, { k: '×', t: 'op' },
    { k: '4', t: 'digit' }, { k: '5', t: 'digit' }, { k: '6', t: 'digit' }, { k: '−', t: 'op' },
    { k: '1', t: 'digit' }, { k: '2', t: 'digit' }, { k: '3', t: 'digit' }, { k: '+', t: 'op' },
    { k: '+/-', t: 'util' }, { k: '0', t: 'digit' }, { k: '.', t: 'digit' }, { k: '=', t: 'eq' },
  ];

  const SCI_LAYOUT = [
    { k: '2nd', t: 'sci' }, { k: 'π', t: 'sci' }, { k: 'e', t: 'sci' }, { k: '⌫', t: 'del' },
    { k: '(', t: 'sci' }, { k: ')', t: 'sci' }, { k: '|x|', t: 'sci' }, { k: 'AC', t: 'util' },
    { k: 'sin', t: 'sci' }, { k: 'cos', t: 'sci' }, { k: 'tan', t: 'sci' }, { k: 'cot', t: 'sci' },
    { k: 'sinh', t: 'sci' }, { k: 'cosh', t: 'sci' }, { k: 'tanh', t: 'sci' }, { k: '%', t: 'util' },
    { k: 'x²', t: 'sci' }, { k: 'x³', t: 'sci' }, { k: 'xʸ', t: 'sci' }, { k: 'log', t: 'sci' },
    { k: '√', t: 'sci' }, { k: '³√', t: 'sci' }, { k: 'ʸ√x', t: 'sci' }, { k: 'ln', t: 'sci' },
    { k: '1/x', t: 'sci' }, { k: 'eˣ', t: 'sci' }, { k: '10ˣ', t: 'sci' }, { k: 'n!', t: 'sci' },
    { k: '7', t: 'digit' }, { k: '8', t: 'digit' }, { k: '9', t: 'digit' }, { k: '÷', t: 'op' },
    { k: '4', t: 'digit' }, { k: '5', t: 'digit' }, { k: '6', t: 'digit' }, { k: '×', t: 'op' },
    { k: '1', t: 'digit' }, { k: '2', t: 'digit' }, { k: '3', t: 'digit' }, { k: '−', t: 'op' },
    { k: '+/-', t: 'util' }, { k: '0', t: 'digit' }, { k: '.', t: 'digit' }, { k: '+', t: 'op' },
    { k: 'EE', t: 'sci' }, { k: 'a⁄b', t: 'sci' }, { k: 'Rad', t: 'sci' }, { k: '=', t: 'eq' },
  ];

  // Fractions mode: digit grid + fraction-input controls. The user types the
  // whole part, presses a/b to open num/den slots and navigates with arrows.
  const FRAC_LAYOUT = [
    { k: 'a/b', t: 'frac' }, { k: 'Mix', t: 'frac' }, { k: '⌫', t: 'del' }, { k: 'AC', t: 'util' },
    { k: '↑', t: 'frac' }, { k: '↓', t: 'frac' }, { k: '%', t: 'util' }, { k: '÷', t: 'op' },
    { k: '←', t: 'frac' }, { k: '→', t: 'frac' }, { k: 'Simp', t: 'frac' }, { k: '×', t: 'op' },
    { k: '7', t: 'digit' }, { k: '8', t: 'digit' }, { k: '9', t: 'digit' }, { k: '−', t: 'op' },
    { k: '4', t: 'digit' }, { k: '5', t: 'digit' }, { k: '6', t: 'digit' }, { k: '+', t: 'op' },
    { k: '1', t: 'digit' }, { k: '2', t: 'digit' }, { k: '3', t: 'digit' }, { k: '=', t: 'eq' },
    { k: '+/-', t: 'util' }, { k: '0', t: 'digit' }, { k: '.', t: 'digit' }, { k: 'D⇄F', t: 'frac' },
  ];

  // When 2nd is on, these keys flip to their inverse counterparts.
  const ALT_KEYS = {
    'sin': 'asin', 'cos': 'acos', 'tan': 'atan',
    'sinh': 'asinh', 'cosh': 'acosh', 'tanh': 'atanh',
    'ln': 'eˣ', 'log': '10ˣ',
    'eˣ': 'ln', '10ˣ': 'log',
    '√': 'x²', 'x²': '√',
    '³√': 'x³', 'x³': '³√',
  };

  const HISTORY_KEY = 'calcPro.history';
  const MEMORY_KEY = 'calcPro.memory';
  const HISTORY_LIMIT = 50;

  // ===================================================================
  // PARSER (shunting-yard, no eval)
  // ===================================================================
  const OPERATORS = {
    '+': { prec: 1, assoc: 'L', fn: (a, b) => a + b },
    '-': { prec: 1, assoc: 'L', fn: (a, b) => a - b },
    '*': { prec: 2, assoc: 'L', fn: (a, b) => a * b },
    '/': { prec: 2, assoc: 'L', fn: (a, b) => { if (b === 0) throw new Error('DIV_ZERO'); return a / b; } },
    '^': { prec: 4, assoc: 'R', fn: (a, b) => Math.pow(a, b) },
    '%': { prec: 2, assoc: 'L', fn: (a, b) => a % b },
    // Prefix unary minus: binds tighter than * and /, looser than ^,
    // so -2^2 = -(2^2) = -4 and 2^-2 = 0.25 (the usual math convention).
    'u-': { prec: 3, assoc: 'R', unary: true, fn: a => -a },
  };

  function toRadFromMode(val, angleMode) {
    if (angleMode === 'deg') return val * Math.PI / 180;
    if (angleMode === 'grad') return val * Math.PI / 200;
    return val;
  }

  function fromRadToMode(val, angleMode) {
    if (angleMode === 'deg') return val * 180 / Math.PI;
    if (angleMode === 'grad') return val * 200 / Math.PI;
    return val;
  }

  function applyFunc(name, val, angleMode) {
    const mode = angleMode || 'deg';
    switch (name) {
      case 'sin': return Math.sin(toRadFromMode(val, mode));
      case 'cos': return Math.cos(toRadFromMode(val, mode));
      case 'tan': {
        const r = Math.tan(toRadFromMode(val, mode));
        if (!isFinite(r) || Math.abs(r) > 1e15) throw new Error('DOMAIN');
        return r;
      }
      case 'cot': {
        const t = Math.tan(toRadFromMode(val, mode));
        if (!isFinite(t) || Math.abs(t) < 1e-15) throw new Error('DOMAIN');
        return 1 / t;
      }
      case 'asin': if (val < -1 || val > 1) throw new Error('DOMAIN'); return fromRadToMode(Math.asin(val), mode);
      case 'acos': if (val < -1 || val > 1) throw new Error('DOMAIN'); return fromRadToMode(Math.acos(val), mode);
      case 'atan': return fromRadToMode(Math.atan(val), mode);
      case 'sinh': return Math.sinh(val);
      case 'cosh': return Math.cosh(val);
      case 'tanh': return Math.tanh(val);
      case 'asinh': return Math.asinh(val);
      case 'acosh': if (val < 1) throw new Error('DOMAIN'); return Math.acosh(val);
      case 'atanh': if (val <= -1 || val >= 1) throw new Error('DOMAIN'); return Math.atanh(val);
      case 'ln': if (val <= 0) throw new Error('DOMAIN'); return Math.log(val);
      case 'log': if (val <= 0) throw new Error('DOMAIN'); return Math.log10(val);
      case 'sqrt': if (val < 0) throw new Error('DOMAIN'); return Math.sqrt(val);
      case 'cbrt': return Math.cbrt(val);
      case 'abs': return Math.abs(val);
      case 'neg': return -val;
      case 'fact': {
        if (val < 0 || !Number.isInteger(val)) throw new Error('DOMAIN');
        if (val > 170) throw new Error('OVERFLOW');
        let r = 1; for (let i = 2; i <= val; i++) r *= i; return r;
      }
      case 'exp': return Math.exp(val);
      case 'exp10': return Math.pow(10, val);
      case 'inv': if (val === 0) throw new Error('DIV_ZERO'); return 1 / val;
      default: throw new Error('UNKNOWN_FN');
    }
  }

  const FUNC_RE = /^(asinh|acosh|atanh|asin|acos|atan|sinh|cosh|tanh|sin|cos|tan|cot|ln|log|sqrt|cbrt|abs|fact|exp10|exp|inv|neg)/;

  function tokenise(expr) {
    const tokens = [];
    const e = expr.trim();
    let i = 0;
    while (i < e.length) {
      const ch = e[i];
      if (/\s/.test(ch)) { i++; continue; }

      // Number (with optional E exponent)
      if (/[0-9]/.test(ch) || (ch === '.' && /[0-9]/.test(e[i + 1] || ''))) {
        let num = '', dots = 0;
        while (i < e.length && /[0-9.]/.test(e[i])) {
          if (e[i] === '.' && ++dots > 1) throw new Error('SYNTAX');
          num += e[i++];
        }
        // 'e' is the constant unless it is followed by a digit or sign (exponent).
        if (e[i] === 'E' || e[i] === 'e') {
          if (/[0-9+\-]/.test(e[i + 1] || '')) {
            num += e[i++];
            if (e[i] === '+' || e[i] === '-') num += e[i++];
            while (i < e.length && /[0-9]/.test(e[i])) num += e[i++];
          }
        }
        tokens.push({ type: 'num', val: parseFloat(num) });
        continue;
      }

      const fm = e.slice(i).match(FUNC_RE);
      if (fm) { tokens.push({ type: 'func', val: fm[1] }); i += fm[1].length; continue; }

      if (ch === 'π') { tokens.push({ type: 'num', val: Math.PI }); i++; continue; }
      if (ch === 'e') { tokens.push({ type: 'num', val: Math.E }); i++; continue; }

      if ('+-*/^%'.includes(ch)) { tokens.push({ type: 'op', val: ch }); i++; continue; }
      if (ch === '(') { tokens.push({ type: 'lparen' }); i++; continue; }
      if (ch === ')') { tokens.push({ type: 'rparen' }); i++; continue; }
      // Unknown character — fail loudly instead of silently producing odd results.
      throw new Error('SYNTAX');
    }
    return tokens;
  }

  function toRPN(tokens) {
    const out = [], ops = [];
    for (let i = 0; i < tokens.length; i++) {
      const tok = tokens[i];
      const prev = tokens[i - 1];

      if (tok.type === 'num') { out.push(tok); continue; }
      if (tok.type === 'func') { ops.push(tok); continue; }
      if (tok.type === 'op') {
        const unaryPos = !prev || prev.type === 'lparen' || prev.type === 'op';
        if (tok.val === '-' && unaryPos) { ops.push({ type: 'op', val: 'u-' }); continue; }
        if (tok.val === '+' && unaryPos) continue;
        const op = OPERATORS[tok.val];
        while (
          ops.length &&
          ops[ops.length - 1].type !== 'lparen' &&
          (
            ops[ops.length - 1].type === 'func' ||
            (ops[ops.length - 1].type === 'op' && (
              OPERATORS[ops[ops.length - 1].val].prec > op.prec ||
              (OPERATORS[ops[ops.length - 1].val].prec === op.prec && op.assoc === 'L')
            ))
          )
        ) { out.push(ops.pop()); }
        ops.push(tok);
        continue;
      }
      if (tok.type === 'lparen') { ops.push(tok); continue; }
      if (tok.type === 'rparen') {
        while (ops.length && ops[ops.length - 1].type !== 'lparen') out.push(ops.pop());
        if (!ops.length) throw new Error('MISMATCH');
        ops.pop();
        if (ops.length && ops[ops.length - 1].type === 'func') out.push(ops.pop());
        continue;
      }
    }
    while (ops.length) {
      const t = ops.pop();
      if (t.type === 'lparen' || t.type === 'rparen') throw new Error('MISMATCH');
      out.push(t);
    }
    return out;
  }

  function evalRPN(rpn, angleMode) {
    const stack = [];
    for (const tok of rpn) {
      if (tok.type === 'num') stack.push(tok.val);
      else if (tok.type === 'func') {
        if (!stack.length) throw new Error('SYNTAX');
        stack.push(applyFunc(tok.val, stack.pop(), angleMode));
      } else if (tok.type === 'op' && OPERATORS[tok.val].unary) {
        if (!stack.length) throw new Error('SYNTAX');
        stack.push(OPERATORS[tok.val].fn(stack.pop()));
      } else if (tok.type === 'op') {
        if (stack.length < 2) throw new Error('SYNTAX');
        const b = stack.pop(), a = stack.pop();
        stack.push(OPERATORS[tok.val].fn(a, b));
      }
    }
    if (stack.length !== 1) throw new Error('SYNTAX');
    return stack[0];
  }

  /**
   * Evaluates an internal-syntax expression ("2*(3+sin(30))").
   * Throws Error with message EMPTY | SYNTAX | MISMATCH | DIV_ZERO | DOMAIN | OVERFLOW.
   */
  function evaluate(s, angleMode) {
    if (!s || !s.trim()) throw new Error('EMPTY');
    const tokens = tokenise(s);
    if (!tokens.length) throw new Error('EMPTY');
    // Implicit multiplication: "2(3)", "(1)(2)", "2π", "2sin(30)".
    const expanded = [];
    for (let i = 0; i < tokens.length; i++) {
      const cur = tokens[i], prev = tokens[i - 1];
      if (prev) {
        const prevEndsValue = prev.type === 'num' || prev.type === 'rparen';
        const curStartsValue = cur.type === 'num' || cur.type === 'lparen' || cur.type === 'func';
        if (prevEndsValue && curStartsValue) expanded.push({ type: 'op', val: '*' });
      }
      expanded.push(cur);
    }
    const r = evalRPN(toRPN(expanded), angleMode || 'deg');
    if (!isFinite(r)) throw new Error('OVERFLOW');
    return r;
  }

  // ===================================================================
  // NUMBERS AND FRACTIONS
  // ===================================================================
  function gcd(a, b) {
    a = Math.abs(a); b = Math.abs(b);
    while (b) { [a, b] = [b, a % b]; }
    return a || 1;
  }

  /** Best rational approximation with denominator ≤ maxDenom (continued fractions). */
  function decimalToFraction(x, eps = 1e-9, maxDenom = 10000) {
    if (!isFinite(x)) return null;
    if (Math.abs(x - Math.round(x)) < eps) return { n: Math.round(x), d: 1 };
    const sign = x < 0 ? -1 : 1;
    const v = Math.abs(x);
    let h1 = 1, h0 = 0, k1 = 0, k0 = 1;
    let b = v;
    let bestN = 0, bestD = 1, bestErr = Infinity;
    for (let iter = 0; iter < 64; iter++) {
      const a = Math.floor(b);
      const h2 = a * h1 + h0;
      const k2 = a * k1 + k0;
      if (k2 > maxDenom) break;
      h0 = h1; h1 = h2;
      k0 = k1; k1 = k2;
      const err = Math.abs(v - h1 / k1);
      if (err < bestErr) { bestErr = err; bestN = h1; bestD = k1; }
      if (err < eps) break;
      const frac = b - a;
      if (frac < eps) break;
      b = 1 / frac;
      if (!isFinite(b)) break;
    }
    if (bestErr > 1e-6) return null;
    const g = gcd(bestN, bestD);
    return { n: sign * bestN / g, d: bestD / g };
  }

  /** "1 1/2", "-3/4" or null when the value is an integer / not a nice fraction. */
  function formatFraction(value) {
    const f = decimalToFraction(value);
    if (!f || f.d === 1) return null;
    const sign = f.n < 0 ? '-' : '';
    const an = Math.abs(f.n);
    const d = f.d;
    if (an > d) {
      const whole = Math.floor(an / d);
      const rem = an - whole * d;
      if (rem === 0) return null;
      return `${sign}${whole} ${rem}/${d}`;
    }
    return `${sign}${an}/${d}`;
  }

  function formatNumber(n) {
    // Coerce defensively — history entries from older builds may have stored strings.
    const num = typeof n === 'number' ? n : parseFloat(n);
    if (!isFinite(num)) return 'Overflow';
    if (Math.abs(num) >= 1e15 || (Math.abs(num) < 1e-10 && num !== 0)) {
      return num.toExponential(6).replace(/\.?0+e/, 'e').replace(/e\+?/, 'e');
    }
    return String(parseFloat(num.toPrecision(12)));
  }

  /** Internal expression → what the display shows ("2*sqrt(" → "2×√("). */
  function displayExpr(expression) {
    return expression
      .replace(/\*/g, '×')
      .replace(/\//g, '÷')
      .replace(/\bneg\(/g, '-(')
      .replace(/\bsqrt\(/g, '√(')
      .replace(/\bcbrt\(/g, '³√(');
  }

  function expressionToInternal(s) {
    return s.replace(/×/g, '*').replace(/÷/g, '/').replace(/−/g, '-');
  }

  // ===================================================================
  // FRACTION TERMS — {whole, num, den, op}
  // ===================================================================
  function emptyTerm() { return { whole: '', num: '', den: '', op: '' }; }

  function escHtml(s) {
    return String(s).replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));
  }

  function renderFractionStack(num, den, isActive, activeSlot) {
    const numCls = (isActive && activeSlot === 'num') ? ' frac-active' : '';
    const denCls = (isActive && activeSlot === 'den') ? ' frac-active' : '';
    const numTxt = num === '' ? '<span class="frac-placeholder">·</span>' : escHtml(num);
    const denTxt = den === '' ? '<span class="frac-placeholder">·</span>' : escHtml(den);
    return (
      '<span class="frac-stack">' +
        '<span class="frac-num' + numCls + '">' + numTxt + '</span>' +
        '<span class="frac-bar"></span>' +
        '<span class="frac-den' + denCls + '">' + denTxt + '</span>' +
      '</span>'
    );
  }

  function renderFracTerm(term, isCurrent, cursorSlot) {
    const hasFrac = term.num !== '' || term.den !== '' || (isCurrent && cursorSlot !== 'whole');
    const wholeCls = (isCurrent && cursorSlot === 'whole') ? ' frac-active' : '';
    const wholeTxt = term.whole === ''
      ? (hasFrac ? '' : '<span class="frac-placeholder">·</span>')
      : escHtml(term.whole);
    let html = '';
    if (wholeTxt || wholeCls) html += '<span class="frac-whole' + wholeCls + '">' + wholeTxt + '</span>';
    if (hasFrac) {
      if (wholeTxt) html += '<span class="frac-gap">&nbsp;</span>';
      html += renderFractionStack(term.num, term.den, isCurrent, cursorSlot);
    }
    return '<span class="frac-term">' + html + '</span>';
  }

  function renderFractionExpr(terms, cursor) {
    const opSymbol = { '+': '+', '-': '−', '*': '×', '/': '÷' };
    let html = '';
    for (let i = 0; i < terms.length; i++) {
      const t = terms[i];
      html += renderFracTerm(t, i === cursor.term, cursor.slot);
      if (t.op) html += '<span class="frac-op">' + opSymbol[t.op] + '</span>';
    }
    return html;
  }

  /**
   * Plain-text version of the fraction expression (for history / the top line).
   *
   * Built from the terms, not by stripping tags from the stacked HTML: that
   * dropped the fraction bar, and 1/2 + 1/3 read as "12 + 13". `cursor` is kept
   * for the call sites; the text does not depend on it.
   */
  function fractionExprText(terms, cursor) {
    const opSymbol = { '+': '+', '-': '−', '*': '×', '/': '÷' };
    const parts = [];
    for (const t of terms) {
      const hasFrac = t.num !== '' || t.den !== '';
      const text = [t.whole, hasFrac ? t.num + '/' + t.den : ''].filter(Boolean).join(' ');
      if (text) parts.push(text);
      if (t.op) parts.push(opSymbol[t.op]);
    }
    return parts.join(' ');
  }

  /**
   * {whole,num,den} → parser substring. Sign convention: -5 1/2 means
   * -(5 + 1/2). A minus on whole or num applies to the whole term; minuses on
   * both slots cancel.
   */
  function termToExpression(term) {
    const stripSign = s => s.startsWith('-') ? { sign: -1, val: s.slice(1) } : { sign: 1, val: s };
    let sign = 1;
    let w = term.whole, n = term.num;
    const d = term.den;
    if (w !== '') { const r = stripSign(w); sign *= r.sign; w = r.val; }
    if (n !== '') { const r = stripSign(n); sign *= r.sign; n = r.val; }
    const prefix = sign < 0 ? '-' : '';
    if (n !== '' && d !== '') {
      if (w !== '') return prefix + '(' + w + '+' + n + '/' + d + ')';
      return prefix + '(' + n + '/' + d + ')';
    }
    if (w !== '') return prefix + w;
    if (n !== '') return prefix + n;
    return '0';
  }

  // ===================================================================
  // CALCULATOR (key-press state machine)
  // ===================================================================
  /**
   * @param {object} [options]
   * @param {Storage} [options.storage]  localStorage-like; history + memory persist here
   * @param {() => number} [options.now] clock for history timestamps
   * @param {object} [options.on] listeners: change, evaluated, history, altset, angle, mode, memflash
   */
  function createCalculator(options) {
    const opts = options || {};
    const storage = opts.storage || null;
    const now = opts.now || (() => Date.now());
    const listeners = opts.on || {};
    const emit = (name, arg) => { const fn = listeners[name]; if (fn) fn(arg); };
    const render = () => emit('change');

    const state = {
      expression: '',
      display: '0',
      historyLine: '',
      result: null,
      memory: 0,
      mode: 'standard',           // 'standard' | 'scientific' | 'fractions'
      angleMode: 'deg',           // 'deg' | 'rad' | 'grad'
      fractionMode: false,
      altSet: false,
      justEvaluated: false,
      calcHistory: [],
      hasError: false,

      // Fractions mode: terms being entered and the cursor {term, slot}.
      fracTerms: [emptyTerm()],
      fracCursor: { term: 0, slot: 'whole' },
      fracResult: null,
      fracResultFrac: null,
    };

    if (storage) {
      try {
        const saved = JSON.parse(storage.getItem(HISTORY_KEY) || '[]');
        if (Array.isArray(saved)) state.calcHistory = saved.filter(h => h && typeof h === 'object');
        const mem = storage.getItem(MEMORY_KEY);
        if (mem) state.memory = parseFloat(mem) || 0;
      } catch (e) { /* corrupted storage: start clean */ }
    }

    function persistHistory() {
      if (!storage) return;
      try { storage.setItem(HISTORY_KEY, JSON.stringify(state.calcHistory.slice(0, HISTORY_LIMIT))); } catch (e) { /* quota */ }
    }
    function persistMemory() {
      if (!storage) return;
      try { storage.setItem(MEMORY_KEY, String(state.memory)); } catch (e) { /* quota */ }
    }
    function pushHistory(entry) {
      state.calcHistory.unshift(entry);
      if (state.calcHistory.length > HISTORY_LIMIT) state.calcHistory.length = HISTORY_LIMIT;
      persistHistory();
      emit('history');
    }

    function setAngleMode(mode) {
      state.angleMode = mode;
      emit('angle');
    }
    function cycleAngleMode() {
      setAngleMode(state.angleMode === 'deg' ? 'rad' : state.angleMode === 'rad' ? 'grad' : 'deg');
    }

    // ----- fractions -----
    function fracResetAll() {
      state.fracTerms = [emptyTerm()];
      state.fracCursor = { term: 0, slot: 'whole' };
      state.fracResult = null;
      state.fracResultFrac = null;
      state.hasError = false;
    }

    function evaluateFractions() {
      // Refuse to silently drop a half-typed fraction (num without den, or vice versa).
      for (const t of state.fracTerms) {
        if ((t.num !== '') !== (t.den !== '')) {
          state.hasError = true;
          state.display = 'Incomplete fraction';
          render();
          return;
        }
      }
      let expr = '';
      for (const t of state.fracTerms) {
        expr += termToExpression(t);
        if (t.op) expr += t.op;
      }
      if (!expr) return;
      try {
        const r = evaluate(expressionToInternal(expr), state.angleMode);
        state.fracResult = r;
        state.fracResultFrac = decimalToFraction(r);
        pushHistory({ expr: fractionExprText(state.fracTerms, state.fracCursor) || 'frac', result: r, ts: now() });
        render();
      } catch (err) {
        state.hasError = true;
        state.display = err.message === 'DIV_ZERO' ? 'Division by zero' :
                        err.message === 'DOMAIN' ? 'Math error' :
                        err.message === 'OVERFLOW' ? 'Overflow' : 'Error';
        render();
      }
    }

    function pressKeyFrac(k) {
      if (state.hasError && k !== 'AC' && k !== '⌫') state.hasError = false;
      // After a result, any digit starts fresh
      if (state.fracResult !== null && /^[0-9.]$/.test(k)) fracResetAll();

      const cur = () => state.fracTerms[state.fracCursor.term];
      const slot = () => state.fracCursor.slot;
      const setSlot = s => { state.fracCursor.slot = s; };

      if (/^[0-9]$/.test(k)) {
        cur()[slot()] += k;
        state.fracResult = null;
        render();
        return;
      }
      if (k === '.') {
        if (slot() === 'whole' && !cur().whole.includes('.')) cur().whole += '.';
        render();
        return;
      }

      // Cursor / slot navigation
      if (k === 'a/b') {
        // whole → num → den → back up to num
        if (slot() === 'whole') setSlot('num');
        else if (slot() === 'num') setSlot('den');
        else setSlot('num');
        render(); return;
      }
      if (k === '↑') { setSlot('num'); render(); return; }
      if (k === '↓') {
        setSlot(slot() === 'whole' ? 'num' : 'den');
        render(); return;
      }
      if (k === '←') {
        if (slot() === 'den') { setSlot('num'); render(); return; }
        if (slot() === 'num') { setSlot('whole'); render(); return; }
        if (slot() === 'whole' && state.fracCursor.term > 0) {
          state.fracCursor.term -= 1;
          const prev = cur();
          setSlot(prev.den !== '' ? 'den' : prev.num !== '' ? 'num' : 'whole');
          render(); return;
        }
        return;
      }
      if (k === '→') {
        if (slot() === 'whole') {
          if (cur().num !== '' || cur().den !== '') setSlot('num');
          else if (state.fracCursor.term < state.fracTerms.length - 1) {
            state.fracCursor.term += 1; setSlot('whole');
          }
          render(); return;
        }
        if (slot() === 'num') { setSlot('den'); render(); return; }
        if (slot() === 'den' && state.fracCursor.term < state.fracTerms.length - 1) {
          state.fracCursor.term += 1; setSlot('whole');
          render(); return;
        }
        return;
      }
      if (k === 'Mix') {
        setSlot(slot() === 'whole' ? 'num' : 'whole');
        render(); return;
      }

      // Operators — finalize current term, append new empty one
      const opMap = { '+': '+', '−': '-', '-': '-', '×': '*', '*': '*', '÷': '/', '/': '/' };
      if (opMap[k] !== undefined) {
        // After =, fold the result into a single starting term, then add the operator.
        if (state.fracResult !== null) {
          const r = state.fracResult;
          const rf = state.fracResultFrac;
          fracResetAll();
          if (rf && rf.d !== 1) {
            // Preserve fraction form so chained ops keep exact arithmetic where possible.
            state.fracTerms[0] = { whole: '', num: String(rf.n), den: String(rf.d), op: opMap[k] };
          } else {
            state.fracTerms[0] = { whole: formatNumber(r), num: '', den: '', op: opMap[k] };
          }
          state.fracTerms.push(emptyTerm());
          state.fracCursor.term = 1;
          state.fracCursor.slot = 'whole';
          render(); return;
        }
        // Current term empty and previous has an op: replace that op instead of
        // stacking a phantom empty term.
        const c = cur();
        const isCurEmpty = c.whole === '' && c.num === '' && c.den === '';
        if (isCurEmpty && state.fracCursor.term > 0) {
          const prev = state.fracTerms[state.fracCursor.term - 1];
          if (prev.op) {
            prev.op = opMap[k];
            render(); return;
          }
        }
        cur().op = opMap[k];
        state.fracTerms.push(emptyTerm());
        state.fracCursor.term = state.fracTerms.length - 1;
        state.fracCursor.slot = 'whole';
        state.fracResult = null;
        render(); return;
      }
      if (k === '+/-') {
        const t = cur();
        if (t[slot()] && t[slot()][0] === '-') t[slot()] = t[slot()].slice(1);
        else if (t[slot()]) t[slot()] = '-' + t[slot()];
        render(); return;
      }
      if (k === '%') {
        // Treat as /100 of current term
        cur().op = '/';
        state.fracTerms.push({ whole: '100', num: '', den: '', op: '' });
        evaluateFractions();
        return;
      }
      if (k === 'AC') { fracResetAll(); render(); return; }
      if (k === '⌫') {
        const t = cur();
        if (t[slot()].length > 0) {
          t[slot()] = t[slot()].slice(0, -1);
        } else if (slot() === 'den') setSlot('num');
        else if (slot() === 'num') setSlot('whole');
        else if (state.fracCursor.term > 0) {
          // Remove current empty term, go back
          state.fracTerms.pop();
          state.fracCursor.term -= 1;
          cur().op = '';
          const p = cur();
          setSlot(p.den !== '' ? 'den' : p.num !== '' ? 'num' : 'whole');
        }
        state.fracResult = null;
        render(); return;
      }
      if (k === '=') {
        // = on an already-evaluated result is a no-op — don't duplicate history.
        if (state.fracResult !== null) return;
        evaluateFractions();
        return;
      }
      if (k === 'Simp') {
        // Improper → mixed, reduce by GCD. Sign on whole or num applies to the whole term.
        const t = cur();
        if (t.num !== '' && t.den !== '') {
          let n = parseInt(t.num, 10), d = parseInt(t.den, 10);
          let w = parseInt(t.whole || '0', 10) || 0;
          if (d !== 0 && Number.isFinite(n) && Number.isFinite(d)) {
            let sign = 1;
            if (w < 0) { sign = -sign; w = -w; }
            if (n < 0) { sign = -sign; n = -n; }
            if (d < 0) d = -d;
            const g = gcd(n, d);
            n = n / g; d = d / g;
            if (n >= d) {
              w += Math.trunc(n / d);
              n = n - Math.trunc(n / d) * d;
            }
            const signed = x => sign < 0 ? '-' + x : String(x);
            if (w !== 0) {
              t.whole = signed(w);
              t.num = n === 0 ? '' : String(n);
            } else {
              t.whole = '';
              t.num = n === 0 ? '' : signed(n);
            }
            t.den = n === 0 ? '' : String(d);
          }
        }
        render(); return;
      }
      if (k === 'D⇄F') {
        state.fractionMode = !state.fractionMode;
        render(); return;
      }
    }

    // ----- standard / scientific -----
    function clearAll() {
      state.expression = '';
      state.display = '0';
      state.historyLine = '';
      state.result = null;
      state.justEvaluated = false;
      state.hasError = false;
      render();
    }

    function doEvaluate() {
      const raw = state.expression.trim();
      if (!raw) return;
      let expr = raw;
      const opens = (expr.match(/\(/g) || []).length;
      const closes = (expr.match(/\)/g) || []).length;
      for (let i = 0; i < opens - closes; i++) expr += ')';
      try {
        const r = evaluate(expressionToInternal(expr), state.angleMode);
        const txt = formatNumber(r);
        pushHistory({ expr: displayExpr(state.expression), result: r, ts: now() });
        state.historyLine = displayExpr(state.expression) + ' =';
        state.display = txt;
        state.result = r;
        state.expression = txt;
        state.justEvaluated = true;
        state.hasError = false;
        render();
        emit('evaluated');
      } catch (err) {
        let msg = 'Error';
        if (err.message === 'DIV_ZERO') msg = "Can't divide by 0";
        else if (err.message === 'OVERFLOW') msg = 'Overflow';
        else if (err.message === 'DOMAIN') msg = 'Not a number';
        else if (err.message === 'MISMATCH' || err.message === 'SYNTAX') msg = 'Syntax error';
        else if (err.message === 'EMPTY') return;
        state.display = msg;
        state.historyLine = displayExpr(state.expression);
        state.hasError = true;
        state.justEvaluated = false;
        state.result = null;
        render();
      }
    }

    // Replaces the running expression with f(result) and evaluates it at once.
    function applyToResult(prefix, suffix) {
      state.expression = prefix + formatNumber(state.result) + suffix;
      state.justEvaluated = false; state.historyLine = ''; state.result = null;
      doEvaluate();
    }

    function startFromResult(expression) {
      state.expression = expression;
      state.justEvaluated = false; state.historyLine = ''; state.result = null;
    }

    function setExpression(expression) {
      state.expression = expression;
      state.display = expression;
      render();
    }

    const FUNC_KEYS = {
      'sin': 'sin(', 'cos': 'cos(', 'tan': 'tan(', 'cot': 'cot(',
      'asin': 'asin(', 'acos': 'acos(', 'atan': 'atan(',
      'sinh': 'sinh(', 'cosh': 'cosh(', 'tanh': 'tanh(',
      'asinh': 'asinh(', 'acosh': 'acosh(', 'atanh': 'atanh(',
      'ln': 'ln(', 'log': 'log(', 'abs': 'abs(', '|x|': 'abs(',
      'eˣ': 'exp(', '10ˣ': 'exp10(',
    };
    const OP_KEYS = { '+': '+', '−': '-', '-': '-', '×': '*', '*': '*', '÷': '/', '/': '/', '^': '^' };

    function pressKey(k) {
      if (state.mode === 'fractions') { pressKeyFrac(k); return; }
      if (state.hasError) {
        if (k === 'AC' || k === '⌫') { clearAll(); return; }
        if (/^[0-9.]$/.test(k)) clearAll(); // then fall through and type the digit
        else return;
      }

      if (k === 'AC') { clearAll(); return; }
      if (k === 'a⁄b') { pressKey('÷'); return; } // fraction divider = friendly alias for ÷

      if (k === '⌫') {
        if (state.justEvaluated) { clearAll(); return; }
        if (state.expression.length > 0) {
          const m = state.expression.match(/(asin|acos|atan|sinh|cosh|tanh|sin|cos|tan|cot|ln|log|sqrt|cbrt|abs|fact|exp10|exp|neg)\($/);
          state.expression = m ? state.expression.slice(0, -m[0].length) : state.expression.slice(0, -1);
          state.display = state.expression || '0';
        }
        state.historyLine = '';
        render();
        return;
      }

      if (/^[0-9]$/.test(k)) {
        if (state.justEvaluated) {
          state.expression = '';
          state.historyLine = '';
          state.justEvaluated = false;
          state.result = null;
        }
        if (state.expression === '0') state.expression = '';
        setExpression(state.expression + k);
        return;
      }

      if (k === '.') {
        if (state.justEvaluated) {
          state.expression = '0';
          state.justEvaluated = false; state.result = null; state.historyLine = '';
        }
        const parts = state.expression.split(/[+\-*/^%(),]/);
        if (parts[parts.length - 1].includes('.')) return;
        if (!state.expression || /[+\-*/^(]$/.test(state.expression)) state.expression += '0';
        setExpression(state.expression + '.');
        return;
      }

      if (k === '=') { doEvaluate(); return; }

      if (k === '+/-') {
        if (state.justEvaluated && state.result !== null) {
          state.result = -state.result;
          setExpression(formatNumber(state.result));
          return;
        }
        const expr = state.expression;
        const unaryMinusAt = (s, i) => s[i] === '-' && (i === 0 || /[+\-*/^(]/.test(s[i - 1]));
        // No operand yet: toggle a leading unary minus ("" ↔ "-", "5×" ↔ "5×-").
        // (1.0 opened "neg(" here, which then swallowed everything typed after it.)
        if (!expr || /[+\-*/^(]$/.test(expr)) {
          setExpression(expr && unaryMinusAt(expr, expr.length - 1) ? expr.slice(0, -1) : expr + '-');
          return;
        }
        // Trailing neg(...) closed — unwrap it.
        const closed = expr.match(/^(.*)neg\(([^()]*)\)$/);
        if (closed) { setExpression(closed[1] + closed[2]); return; }
        // Trailing number: drop its unary minus, or wrap it in neg(...).
        const m = expr.match(/(.*?)(\d+\.?\d*(?:E[+-]?\d+)?)$/);
        if (m) {
          const before = m[1], num = m[2];
          if (before.endsWith('neg(')) setExpression(before.slice(0, -4) + num);
          else if (unaryMinusAt(before, before.length - 1)) setExpression(before.slice(0, -1) + num);
          else setExpression(before + 'neg(' + num + ')');
          return;
        }
        setExpression('neg(' + expr + ')');
        return;
      }

      if (OP_KEYS[k] !== undefined) {
        const op = OP_KEYS[k];
        if (state.justEvaluated && state.result !== null) {
          state.expression = formatNumber(state.result) + op;
          state.justEvaluated = false;
          state.historyLine = '';
        } else if (state.expression === '') {
          state.expression = (op === '-') ? '-' : '0' + op;
        } else if (/[+\-*/^]$/.test(state.expression)) {
          state.expression = state.expression.slice(0, -1) + op;
        } else {
          state.expression += op;
        }
        setExpression(state.expression);
        return;
      }

      // % — context-aware, Apple/Windows style:
      //   a + b % → a + (a*b/100)   a - b % → a - (a*b/100)
      //   a * b % → a * (b/100)     a / b % → a / (b/100)
      //   b %     → b/100           <result> % after = → result/100
      if (k === '%') {
        if (!state.expression && state.result === null) return;
        if (state.justEvaluated && state.result !== null) {
          state.result = state.result / 100;
          setExpression(formatNumber(state.result));
          return;
        }
        // Split on the LAST top-level + - * / operator.
        const expr = state.expression;
        let depth = 0, splitIdx = -1, splitOp = '';
        for (let i = expr.length - 1; i >= 0; i--) {
          const c = expr[i];
          if (c === ')') depth++;
          else if (c === '(') depth--;
          else if (depth === 0 && /[+\-*/]/.test(c) && i > 0 && !/[+\-*/^(]/.test(expr[i - 1])) {
            splitIdx = i; splitOp = c; break;
          }
        }
        try {
          if (splitIdx >= 0) {
            const left = expr.slice(0, splitIdx);
            const right = expr.slice(splitIdx + 1);
            if (!right) return; // % right after an operator — no-op
            const leftVal = evaluate(expressionToInternal(left), state.angleMode);
            const rightVal = evaluate(expressionToInternal(right), state.angleMode);
            const replacement = (splitOp === '+' || splitOp === '-')
              ? formatNumber(leftVal * rightVal / 100)
              : formatNumber(rightVal / 100);
            state.expression = left + splitOp + replacement;
          } else {
            state.expression = formatNumber(evaluate(expressionToInternal(expr), state.angleMode) / 100);
          }
        } catch (e) {
          state.expression = '(' + expr + ')/100';
        }
        setExpression(state.expression);
        return;
      }

      if (k === '(' || k === ')') {
        if (state.justEvaluated) {
          if (k === ')') return;
          startFromResult('');
        }
        setExpression(state.expression + k);
        return;
      }

      if (k === 'EE') {
        // After "=" the result becomes the mantissa: 5 = EE 3 = → 5000. Before this
        // the E was glued on while justEvaluated stayed set, so the display showed
        // "0E" for result 0 and the next digit wiped it (found by the random-keys
        // property). A result already in e-notation or an error takes no exponent.
        if (state.justEvaluated) {
          const r = state.result !== null ? formatNumber(state.result) : '';
          if (!/^-?\d+(\.\d+)?$/.test(r)) return;
          startFromResult(r);
        }
        // Exponent marker only makes sense right after a digit.
        if (/\d$/.test(state.expression)) state.expression += 'E';
        setExpression(state.expression);
        return;
      }

      if (FUNC_KEYS[k] !== undefined) {
        if (state.justEvaluated && state.result !== null) { applyToResult(FUNC_KEYS[k], ')'); return; }
        setExpression(state.expression + FUNC_KEYS[k]);
        return;
      }

      if (k === '√' || k === '³√') {
        const fn = k === '√' ? 'sqrt(' : 'cbrt(';
        if (state.justEvaluated && state.result !== null) { applyToResult(fn, ')'); return; }
        setExpression(state.expression + fn);
        return;
      }

      // ʸ√x — yth root: a ʸ√x b → a^(1/b)
      if (k === 'ʸ√x') {
        if (!state.expression && state.result === null) return;
        if (state.justEvaluated && state.result !== null) startFromResult(formatNumber(state.result) + '^(1/');
        else state.expression += '^(1/';
        setExpression(state.expression);
        return;
      }

      if (k === 'n!' || k === 'x!') {
        if (state.justEvaluated && state.result !== null) { applyToResult('fact(', ')'); return; }
        if (!state.expression) return;
        const m = state.expression.match(/^(.*?)(\d+\.?\d*)$/);
        setExpression(m ? m[1] + 'fact(' + m[2] + ')' : 'fact(' + state.expression + ')');
        return;
      }

      if (k === '2nd') {
        state.altSet = !state.altSet;
        emit('altset');
        return;
      }

      if (k === 'Rad') { cycleAngleMode(); return; }

      if (k === 'x²' || k === 'x³') {
        const n = k === 'x²' ? '2' : '3';
        if (state.justEvaluated && state.result !== null) { applyToResult('', '^' + n); return; }
        if (!state.expression) return;
        setExpression(state.expression + '^' + n);
        return;
      }

      if (k === 'xʸ') {
        if (state.justEvaluated && state.result !== null) startFromResult(formatNumber(state.result) + '^');
        else {
          if (!state.expression) return;
          state.expression += '^';
        }
        setExpression(state.expression);
        return;
      }

      if (k === '1/x') {
        if (state.justEvaluated && state.result !== null) { applyToResult('1/(', ')'); return; }
        if (!state.expression) return;
        setExpression('1/(' + state.expression + ')');
        return;
      }

      if (k === 'π' || k === 'e') {
        if (state.justEvaluated) startFromResult('');
        // Implicit multiplication if last is digit or close-paren
        if (/[\d)]$/.test(state.expression)) state.expression += '*';
        setExpression(state.expression + k);
        return;
      }
    }

    // ----- memory -----
    function memValue() {
      if (state.result !== null) return state.result;
      const v = parseFloat(state.expression);
      return isFinite(v) ? v : 0;
    }
    function memAdd() { state.memory += memValue(); persistMemory(); emit('memflash', 'plus'); }
    function memSub() { state.memory -= memValue(); persistMemory(); emit('memflash', 'minus'); }
    function memStore() { state.memory = memValue(); persistMemory(); emit('memflash', 'store'); }
    function memClear() { state.memory = 0; persistMemory(); emit('memflash', 'clear'); }

    // Puts a ready number into the running expression (MR, history click).
    function insertValue(text, resultValue) {
      if (state.justEvaluated || state.hasError) {
        state.expression = text;
        state.justEvaluated = false; state.hasError = false; state.historyLine = '';
        state.result = resultValue;
      } else if (state.expression === '' || /[+\-*/^(]$/.test(state.expression)) {
        state.expression += text;
      } else {
        state.expression = text;
      }
      setExpression(state.expression);
    }
    function memRecall() {
      insertValue(formatNumber(state.memory), null);
      emit('memflash', 'recall');
    }

    // ----- history -----
    function useHistoryEntry(index) {
      const entry = state.calcHistory[index];
      if (!entry) return;
      insertValue(formatNumber(entry.result), entry.result);
    }
    function clearHistory() {
      state.calcHistory = [];
      persistHistory();
      emit('history');
    }

    // ----- modes -----
    function setMode(mode) {
      if (mode === 'fraction') mode = 'fractions';
      if (mode === 'fractions') {
        fracResetAll();
        state.fractionMode = true; // fractions mode shows results as fractions by default
      } else {
        state.fractionMode = false;
      }
      state.mode = mode;
      emit('mode');
      render();
    }

    function toggleMode() {
      setMode(state.mode === 'scientific' ? 'standard' : 'scientific');
    }

    return {
      state,
      pressKey,
      setMode,
      toggleMode,
      setAngleMode,
      cycleAngleMode,
      memAdd,
      memSub,
      memStore,
      memClear,
      memRecall,
      useHistoryEntry,
      clearHistory,
      clearAll,
    };
  }

  return {
    STD_LAYOUT,
    SCI_LAYOUT,
    FRAC_LAYOUT,
    ALT_KEYS,
    HISTORY_KEY,
    MEMORY_KEY,
    HISTORY_LIMIT,
    OPERATORS,
    applyFunc,
    tokenise,
    toRPN,
    evalRPN,
    evaluate,
    gcd,
    decimalToFraction,
    formatFraction,
    formatNumber,
    displayExpr,
    expressionToInternal,
    escHtml,
    renderFractionStack,
    renderFracTerm,
    renderFractionExpr,
    fractionExprText,
    termToExpression,
    createCalculator,
  };
}));
