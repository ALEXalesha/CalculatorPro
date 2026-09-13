/*
 * Calculator iOS 26 — calculator engine.
 *
 * Immediate-execution logic modelled on the iPhone calculator ("two fields"):
 *   currentExpr    — white line at the bottom, grows as you type: "2", "2 + ", "2 + 3"
 *   lastExpression — grey line on top, filled on "=" or when an operation chains
 * Numbers are kept already formatted the Russian way: "1 234,56".
 *
 * Pure logic, no DOM. The page (app.js) renders `engine.state` and forwards
 * button presses. Node tests require() this file.
 * UMD: exposes `CalcEngine` on the page, `module.exports` under Node.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.CalcEngine = factory();
}(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  const ERROR = 'Ошибка';
  const HISTORY_KEY = 'calc_history_v1';
  const HISTORY_LIMIT = 200;

  function formatNumber(n) {
    if (!isFinite(n)) return ERROR;
    if (Number.isInteger(n) && Math.abs(n) < 1e16) return n.toLocaleString('ru-RU').replace(/,/g, ' ');
    const abs = Math.abs(n);
    // Decimal comma in the exponent form too ("1,000000e-7"), so the number stays
    // readable by getLastNumber/parseDisplay like every other display number.
    if (abs !== 0 && (abs < 1e-6 || abs >= 1e16)) return n.toExponential(6).replace('.', ',');
    const s = parseFloat(n.toPrecision(12)).toString();
    if (s.includes('.')) {
      const [intPart, decPart] = s.split('.');
      return Number(intPart).toLocaleString('ru-RU').replace(/,/g, ' ') + ',' + decPart;
    }
    return Number(s).toLocaleString('ru-RU').replace(/,/g, ' ');
  }

  const opSymbol = op => ({ '+': '+', '-': '−', '*': '×', '/': '÷', '^': '^' }[op] || op);
  const parseDisplay = s => parseFloat(s.replace(/\s/g, '').replace(',', '.'));

  function compute(a, b, op) {
    if (op === '+') return a + b;
    if (op === '-') return a - b;
    if (op === '*') return a * b;
    if (op === '/') return b === 0 ? NaN : a / b;
    if (op === '^') return Math.pow(a, b);
    return b;
  }

  function factorial(n) {
    if (n < 0 || !Number.isInteger(n) || n > 170) return NaN;
    let r = 1;
    for (let k = 2; k <= n; k++) r *= k;
    return r;
  }

  // Last number at the end of the expression, thousands separators included.
  // toLocaleString('ru-RU') puts NBSP between thousands while operators are
  // wrapped in ordinary spaces; a plain space is accepted as a fallback.
  // First alternative: grouped number ("1 234,56"); second: plain ("12345", "0,").
  // Third form: exponent notation for very large / small values ("-1,5e+20").
  const LAST_NUMBER_RE = /(-?\d{1,3}(?:[  ]\d{3})+(?:,\d*)?|-?\d+(?:,\d*)?e[+-]\d+|-?\d+(?:,\d*)?)$/;
  const isExponent = s => s.includes('e');
  const ENDS_WITH_OP_RE = / [+−×÷^] $/;

  /**
   * @param {object} [options]
   * @param {Storage} [options.storage]  localStorage-like, history persists here
   * @param {() => number} [options.now]
   * @param {() => number} [options.random] used by the Rand key
   * @param {(state) => void} [options.onChange] called after every state change
   */
  function createEngine(options) {
    const opts = options || {};
    const storage = opts.storage || null;
    const now = opts.now || (() => Date.now());
    const random = opts.random || Math.random;
    const onChange = opts.onChange || (() => {});

    const state = {
      currentExpr: '0',
      lastExpression: '',
      firstOperand: null,
      pendingOperator: null,
      isInputtingNew: true,
      isResultShown: false,
      memory: 0,
      isSecond: false,
      isRad: false,
      history: [],
    };

    if (storage) {
      try {
        const saved = JSON.parse(storage.getItem(HISTORY_KEY) || '[]');
        if (Array.isArray(saved)) state.history = saved.filter(h => h && typeof h.expr === 'string');
      } catch (e) { /* corrupted storage: start clean */ }
    }

    const update = () => onChange(state);

    const getLastNumber = () => {
      const m = state.currentExpr.match(LAST_NUMBER_RE);
      return m ? m[1] : '';
    };
    const replaceLastNumber = newNum => {
      const last = getLastNumber();
      if (last) state.currentExpr = state.currentExpr.slice(0, -last.length) + newNum;
      else state.currentExpr += newNum;
    };
    const endsWithOperator = () => ENDS_WITH_OP_RE.test(state.currentExpr);

    // ----- history -----
    function saveHistory() {
      if (!storage) return;
      try { storage.setItem(HISTORY_KEY, JSON.stringify(state.history.slice(-HISTORY_LIMIT))); } catch (e) { /* quota */ }
    }
    function addHistoryEntry(expr, result) {
      if (!expr || !result || result === ERROR) return;
      state.history.push({ expr, result, ts: now() });
      if (state.history.length > HISTORY_LIMIT) state.history = state.history.slice(-HISTORY_LIMIT);
      saveHistory();
    }
    function clearHistory() {
      state.history = [];
      saveHistory();
      update();
    }
    /** Loads a history result as the starting number for further calculations. */
    function useHistoryEntry(index) {
      const h = state.history[index];
      if (!h) return;
      state.currentExpr = h.result;
      state.lastExpression = h.expr + ' =';
      state.firstOperand = null;
      state.pendingOperator = null;
      state.isInputtingNew = true;
      state.isResultShown = true;
      update();
    }

    // ----- input -----
    function inputNum(n) {
      if (state.isResultShown) {
        state.currentExpr = (n === '.') ? '0,' : n;
        state.lastExpression = '';
        state.firstOperand = null;
        state.pendingOperator = null;
        state.isResultShown = false;
        state.isInputtingNew = false;
        update();
        return;
      }

      if (state.isInputtingNew) {
        // Look at the last number, not the whole string: after C or ⌫ the
        // expression may be "5 + 0" and the digit must replace that 0.
        const last = getLastNumber();
        if (last === '') state.currentExpr += (n === '.') ? '0,' : n;
        else if (last === '-0') replaceLastNumber((n === '.') ? '-0,' : '-' + n);
        else replaceLastNumber((n === '.') ? '0,' : n);
        state.isInputtingNew = false;
      } else {
        const last = getLastNumber();
        if (isExponent(last)) {
          // "1,5e+20" is a computed value, not something being typed: start over.
          replaceLastNumber((n === '.') ? '0,' : n);
        } else if (n === '.') {
          if (last.includes(',')) return;
          state.currentExpr += ',';
        } else if (last === '0') replaceLastNumber(n);
        else if (last === '-0') replaceLastNumber('-' + n);
        else state.currentExpr += n;
      }
      update();
    }

    function inputOp(op) {
      // After "Ошибка" an operator only resets, so "Ошибка + 5" never happens.
      if (state.currentExpr === ERROR) { clearAll(); return; }

      if (op === '=') {
        if (state.pendingOperator !== null && state.firstOperand !== null && !state.isInputtingNew) {
          const b = parseDisplay(getLastNumber());
          const result = compute(state.firstOperand, b, state.pendingOperator);
          const exprForHistory = state.currentExpr.trim();
          state.lastExpression = exprForHistory + ' =';
          state.currentExpr = formatNumber(result);
          addHistoryEntry(exprForHistory, state.currentExpr);
          state.firstOperand = null;
          state.pendingOperator = null;
          state.isResultShown = true;
          state.isInputtingNew = true;
        }
        update();
        return;
      }

      if (state.isResultShown) {
        state.currentExpr = state.currentExpr + ' ' + opSymbol(op) + ' ';
        state.firstOperand = parseDisplay(state.currentExpr);
        state.pendingOperator = op;
        state.lastExpression = '';
        state.isResultShown = false;
        state.isInputtingNew = true;
        update();
        return;
      }

      if (endsWithOperator()) {
        state.currentExpr = state.currentExpr.slice(0, -3) + ' ' + opSymbol(op) + ' ';
        state.pendingOperator = op;
        update();
        return;
      }

      if (state.firstOperand === null) {
        state.firstOperand = parseDisplay(getLastNumber());
        state.currentExpr += ' ' + opSymbol(op) + ' ';
        state.pendingOperator = op;
        state.isInputtingNew = true;
        update();
        return;
      }

      const b = parseDisplay(getLastNumber());
      const result = compute(state.firstOperand, b, state.pendingOperator);
      if (!isFinite(result)) {
        // "5 ÷ 0 +" — show the error on its own instead of "Ошибка + ".
        state.lastExpression = state.currentExpr.trim();
        state.currentExpr = ERROR;
        state.firstOperand = null;
        state.pendingOperator = null;
        state.isResultShown = true;
        state.isInputtingNew = true;
        update();
        return;
      }
      state.lastExpression = state.currentExpr.trim();
      state.currentExpr = formatNumber(result) + ' ' + opSymbol(op) + ' ';
      state.firstOperand = result;
      state.pendingOperator = op;
      state.isInputtingNew = true;
      update();
    }

    function clearAll() {
      state.currentExpr = '0';
      state.lastExpression = '';
      state.firstOperand = null;
      state.pendingOperator = null;
      state.isResultShown = false;
      state.isInputtingNew = true;
      update();
    }

    function clearEntry() {
      if (state.isResultShown) { clearAll(); return; }
      const last = getLastNumber();
      if (last === '0' || last === '-0' || !last) { clearAll(); return; }
      replaceLastNumber('0');
      state.isInputtingNew = true;
      update();
    }

    /** The C / AC key: first press clears the entry, second clears everything. */
    function pressClear() {
      if (state.currentExpr !== '0' || state.lastExpression) clearEntry();
      else clearAll();
    }

    function backspace() {
      if (state.isResultShown) { clearAll(); return; }
      if (endsWithOperator()) return;
      const last = getLastNumber();
      if (!last) return;
      if (isExponent(last) || last.length <= 1 || (last.length === 2 && last.startsWith('-'))) {
        if (state.currentExpr.length === last.length) state.currentExpr = '0';
        else replaceLastNumber('0');
        state.isInputtingNew = true;
      } else {
        state.currentExpr = state.currentExpr.slice(0, -1);
      }
      update();
    }

    function negate() {
      if (state.isResultShown) {
        state.currentExpr = formatNumber(-parseDisplay(state.currentExpr));
        update();
        return;
      }
      if (state.isInputtingNew && endsWithOperator()) {
        state.currentExpr += '-0';
        state.isInputtingNew = false;
        update();
        return;
      }
      const last = getLastNumber();
      if (!last) return;
      replaceLastNumber(last.startsWith('-') ? last.slice(1) : '-' + last);
      update();
    }

    function applyPercent() {
      if (state.isInputtingNew || endsWithOperator()) return;
      const v = parseDisplay(getLastNumber());
      const result = (state.firstOperand !== null && (state.pendingOperator === '+' || state.pendingOperator === '-'))
        ? state.firstOperand * v / 100
        : v / 100;
      replaceLastNumber(formatNumber(result));
      update();
    }

    function applyUnary(fn, label) {
      const last = getLastNumber();
      if (!last) return;
      const v = parseDisplay(last);
      const result = fn(v);
      if (!isFinite(result)) {
        state.currentExpr = ERROR;
        state.isInputtingNew = true;
        state.isResultShown = true;
        update();
        return;
      }
      const exprForHistory = `${label}(${formatNumber(v)})`;
      state.lastExpression = exprForHistory;
      state.currentExpr = formatNumber(result);
      addHistoryEntry(exprForHistory, state.currentExpr);
      state.firstOperand = null;
      state.pendingOperator = null;
      state.isInputtingNew = true;
      state.isResultShown = true;
      update();
    }

    const trig = (fn, x) => fn(state.isRad ? x : x * Math.PI / 180);
    const atrig = (fn, x) => state.isRad ? fn(x) : fn(x) * 180 / Math.PI;

    function insertConst(val) {
      if (state.isResultShown) {
        state.currentExpr = formatNumber(val);
        state.lastExpression = '';
        state.isResultShown = false;
        state.isInputtingNew = false;
      } else if (state.isInputtingNew) {
        // "0" (fresh) or "5 + 0" (after C) holds a placeholder zero: replace it.
        // (1.0 appended here, showing "03,14…" for π on a fresh screen.)
        if (getLastNumber()) replaceLastNumber(formatNumber(val));
        else state.currentExpr += formatNumber(val);
        state.isInputtingNew = false;
      } else {
        replaceLastNumber(formatNumber(val));
      }
      update();
    }

    const sciActions = {
      'sinh': () => applyUnary(x => state.isSecond ? Math.asinh(x) : Math.sinh(x), state.isSecond ? 'asinh' : 'sinh'),
      'cosh': () => applyUnary(x => state.isSecond ? Math.acosh(x) : Math.cosh(x), state.isSecond ? 'acosh' : 'cosh'),
      'mc': () => { state.memory = 0; update(); },
      'm+': () => { const last = getLastNumber(); if (last) state.memory += parseDisplay(last); update(); },
      'm-': () => { const last = getLastNumber(); if (last) state.memory -= parseDisplay(last); update(); },
      'mr': () => insertConst(state.memory),
      '2nd': () => { state.isSecond = !state.isSecond; update(); },
      'square': () => applyUnary(x => x * x, 'sqr'),
      'cube': () => applyUnary(x => x * x * x, 'cube'),
      'pow': () => inputOp('^'),
      'exp': () => applyUnary(Math.exp, 'exp'),
      'ten-pow': () => applyUnary(x => Math.pow(10, x), '10^'),
      'recip': () => applyUnary(x => 1 / x, '1/'),
      'sqrt': () => applyUnary(Math.sqrt, '√'),
      'cbrt': () => applyUnary(Math.cbrt, '∛'),
      'ln': () => applyUnary(Math.log, 'ln'),
      'log10': () => applyUnary(Math.log10, 'log'),
      'fact': () => applyUnary(factorial, 'fact'),
      'sin': () => applyUnary(x => state.isSecond ? atrig(Math.asin, x) : trig(Math.sin, x), state.isSecond ? 'asin' : 'sin'),
      'cos': () => applyUnary(x => state.isSecond ? atrig(Math.acos, x) : trig(Math.cos, x), state.isSecond ? 'acos' : 'cos'),
      'tan': () => applyUnary(x => state.isSecond ? atrig(Math.atan, x) : trig(Math.tan, x), state.isSecond ? 'atan' : 'tan'),
      'rad': () => { state.isRad = !state.isRad; update(); },
      'pi': () => insertConst(Math.PI),
      'e': () => insertConst(Math.E),
      'rand': () => {
        const v = random();
        insertConst(v);
        addHistoryEntry('Rand', formatNumber(v));
      },
    };

    /** Runs a scientific-pad action by its data-act name. Unknown names are ignored. */
    function sci(action) {
      const fn = sciActions[action];
      if (fn) fn();
    }

    return {
      state,
      inputNum,
      inputOp,
      clearAll,
      clearEntry,
      pressClear,
      backspace,
      negate,
      applyPercent,
      sci,
      sciActionNames: Object.keys(sciActions),
      getLastNumber,
      clearLabel: () => (state.currentExpr !== '0' || state.lastExpression) ? 'C' : 'AC',
      /** Operator key to highlight (the pending one, while waiting for the next number). */
      activeOperator: () => (state.pendingOperator && endsWithOperator()) ? state.pendingOperator : null,
      useHistoryEntry,
      clearHistory,
    };
  }

  return {
    ERROR,
    HISTORY_KEY,
    HISTORY_LIMIT,
    formatNumber,
    parseDisplay,
    compute,
    factorial,
    opSymbol,
    createEngine,
  };
}));
