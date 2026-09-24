/*
 * Calc Pro Glass — режим «Программист» (1.6.0).
 *
 * Целые 64 бита со знаком, как QWORD в калькуляторе Windows: число вводится в одной
 * системе и сразу видно во всех четырёх, переполнение заворачивается по модулю 2^64,
 * отрицательные в HEX, OCT и BIN - дополнительный код. Числа - BigInt, обрезанные
 * BigInt.asIntN(64): у обычных чисел JS точных целых всего 53 бита.
 *
 * Та же логика - в Calc Pro на C# (CalcPro.Core/Services/ProgrammerCalculator.cs). Обе
 * версии проверяются одним набором примеров (test-vectors/programmer.json).
 *
 * UMD: `ProgrammerCore` на странице, `module.exports` под Node.
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.ProgrammerCore = factory();
}(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  const DIVIDE_BY_ZERO = 'Cannot divide by zero';
  const BASES = { HEX: 16, DEC: 10, OCT: 8, BIN: 2 };
  const PRECEDENCE = {
    '×': 7, '÷': 7, 'Mod': 7,
    '+': 6, '−': 6,
    '<<': 5, '>>': 5,
    'AND': 4,
    'XOR': 3,
    'OR': 2,
  };
  const MIN = -(1n << 63n);
  const wrap = (x) => BigInt.asIntN(64, x);

  // Клавиши на экране: пять столбцов, семь рядов (как в C#-версии).
  const PROG_LAYOUT = [
    { k: 'AND', t: 'sci' }, { k: 'OR', t: 'sci' }, { k: 'XOR', t: 'sci' }, { k: 'NOT', t: 'sci' }, { k: 'Mod', t: 'sci' },
    { k: 'A', t: 'hex' }, { k: '<<', t: 'sci' }, { k: '>>', t: 'sci' }, { k: 'AC', t: 'util' }, { k: '⌫', t: 'del' },
    { k: 'B', t: 'hex' }, { k: '(', t: 'sci' }, { k: ')', t: 'sci' }, { k: '±', t: 'util' }, { k: '÷', t: 'op' },
    { k: 'C', t: 'hex' }, { k: '7', t: 'digit' }, { k: '8', t: 'digit' }, { k: '9', t: 'digit' }, { k: '×', t: 'op' },
    { k: 'D', t: 'hex' }, { k: '4', t: 'digit' }, { k: '5', t: 'digit' }, { k: '6', t: 'digit' }, { k: '−', t: 'op' },
    { k: 'E', t: 'hex' }, { k: '1', t: 'digit' }, { k: '2', t: 'digit' }, { k: '3', t: 'digit' }, { k: '+', t: 'op' },
    { k: 'F', t: 'hex' }, { k: 'CE', t: 'util' }, { k: '0', t: 'digit' }, { k: '=', t: 'eq', span: 2 },
  ];

  /** Значение цифры 0-9, A-F (любой регистр); -1 - не цифра. */
  function digitValue(c) {
    if (typeof c !== 'string' || c.length !== 1) return -1;
    const code = c.charCodeAt(0);
    if (code >= 48 && code <= 57) return code - 48;
    if (code >= 65 && code <= 70) return code - 55;
    if (code >= 97 && code <= 102) return code - 87;
    return -1;
  }

  /** Число в системе: DEC со знаком, остальные - дополнительный код, цифры заглавные. */
  function format(v, base) {
    if (base === 10) return v.toString(10);
    return BigInt.asUintN(64, v).toString(base).toUpperCase();
  }

  /** Разбор записи; не помещается в 64 бита или не цифра - null. Пробелы пропускаются. */
  function parse(text, base) {
    let s = String(text == null ? '' : text).replace(/ /g, '');
    const negative = base === 10 && s.startsWith('-');
    if (negative) s = s.slice(1);
    if (!s.length) return null;
    let acc = 0n;
    const b = BigInt(base);
    for (const c of s) {
      const d = digitValue(c);
      if (d < 0 || d >= base) return null;
      acc = acc * b + BigInt(d);
      if (acc > 0xFFFFFFFFFFFFFFFFn) return null;
    }
    if (base === 10) {
      if (negative ? acc > (1n << 63n) : acc > (1n << 63n) - 1n) return null;
      return negative ? -acc : acc;
    }
    return BigInt.asIntN(64, acc);
  }

  /** Разбивка для глаз: BIN и HEX по 4, OCT и DEC по 3, справа налево. */
  function group(digits, base) {
    const sign = digits.startsWith('-') ? '-' : '';
    const s = sign ? digits.slice(1) : digits;
    const size = base === 2 || base === 16 ? 4 : 3;
    let out = '';
    for (let i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % size === 0) out += ' ';
      out += s[i];
    }
    return sign + out;
  }

  /** Одна операция; null - деление на ноль. */
  function apply(op, a, b) {
    switch (op) {
      case '+': return wrap(a + b);
      case '−': return wrap(a - b);
      case '×': return wrap(a * b);
      case '÷':
        if (b === 0n) return null;
        return a === MIN && b === -1n ? MIN : wrap(a / b);
      case 'Mod':
        if (b === 0n) return null;
        return b === -1n ? 0n : a % b;
      case 'AND': return a & b;
      case 'OR': return a | b;
      case 'XOR': return a ^ b;
      case '<<': return wrap(a << BigInt.asUintN(6, b));
      case '>>': return a >> BigInt.asUintN(6, b);
      default: throw new Error('Неизвестная операция ' + op);
    }
  }

  /** Скобочное выражение с приоритетами (сортировочная станция); null - деление на ноль. */
  function evaluate(tokens) {
    const values = [];
    const ops = [];
    const reduce = () => {
      const op = ops.pop().op;
      const b = values.pop();
      const a = values.pop();
      const r = apply(op, a, b);
      if (r === null) return false;
      values.push(r);
      return true;
    };
    for (const t of tokens) {
      if (t.num !== undefined) values.push(t.num);
      else if (t.open) ops.push(t);
      else if (t.close) {
        while (ops.length && ops[ops.length - 1].op) if (!reduce()) return null;
        if (ops.length) ops.pop();
      } else {
        while (ops.length && ops[ops.length - 1].op && PRECEDENCE[ops[ops.length - 1].op] >= PRECEDENCE[t.op]) {
          if (!reduce()) return null;
        }
        ops.push(t);
      }
    }
    while (ops.length) {
      if (!ops[ops.length - 1].op) { ops.pop(); continue; }
      if (!reduce()) return null;
    }
    return values.length ? values[values.length - 1] : 0n;
  }

  function createProgrammer() {
    const s = {
      base: 10,
      value: 0n,
      error: null,
      tokens: [],
      entry: '',
      justEvaluated: false,
      lastExpression: '',
    };

    const last = () => s.tokens[s.tokens.length - 1];
    const lastIsClose = () => s.entry === '' && s.tokens.length > 0 && !!last().close;
    const lastIsNum = () => s.entry === '' && s.tokens.length > 0 && last().num !== undefined;
    const fresh = () => { s.tokens = []; s.justEvaluated = false; s.lastExpression = ''; };
    const pushEntry = () => { s.tokens.push({ num: s.value }); s.entry = ''; };
    const openCount = () => s.tokens.filter(t => t.open).length - s.tokens.filter(t => t.close).length;

    function clear() {
      s.tokens = []; s.entry = ''; s.value = 0n; s.error = null;
      s.justEvaluated = false; s.lastExpression = '';
    }

    function setBase(b) {
      s.base = b;
      if (s.entry) s.entry = format(s.value, b);
      if (s.justEvaluated) s.lastExpression = '';
    }

    function digit(c) {
      const d = digitValue(c);
      if (d < 0 || d >= s.base) return;
      if (s.justEvaluated) fresh();
      if (lastIsClose()) return;
      const head = s.entry === '0' ? '' : s.entry;
      const candidate = head + c.toUpperCase();
      const v = parse(candidate, s.base);
      if (v === null) return;
      s.entry = candidate;
      s.value = v;
    }

    function backspace() {
      if (!s.entry) return;
      s.entry = s.entry.slice(0, -1);
      if (s.entry === '-') s.entry = '';
      const v = s.entry ? parse(s.entry, s.base) : 0n;
      s.value = v === null ? 0n : v;
    }

    function unary(f) {
      if (s.justEvaluated) fresh();
      if (lastIsClose()) return;
      s.value = f(s.value);
      s.entry = format(s.value, s.base);
    }

    function operator(op) {
      if (s.justEvaluated) fresh();
      if (s.entry) pushEntry();
      else if (!s.tokens.length) s.tokens.push({ num: s.value });
      else if (last().op) { s.tokens[s.tokens.length - 1] = { op }; return; }
      else if (last().open) return;
      s.tokens.push({ op });
    }

    function openParen() {
      if (s.justEvaluated) { fresh(); s.value = 0n; }
      if (s.entry || lastIsClose() || lastIsNum()) return;
      s.tokens.push({ open: true });
    }

    function closeParen() {
      if (s.justEvaluated || openCount() <= 0) return;
      if (s.entry) pushEntry();
      else if (last().op) s.tokens.push({ num: s.value });
      else if (last().open) return;
      s.tokens.push({ close: true });
      let depth = 0, start = 0;
      for (let i = s.tokens.length - 1; i >= 0; i--) {
        if (s.tokens[i].close) depth++;
        else if (s.tokens[i].open && --depth === 0) { start = i; break; }
      }
      const v = evaluate(s.tokens.slice(start + 1, s.tokens.length - 1));
      if (v !== null) s.value = v;
    }

    function equals() {
      if (s.justEvaluated) return;
      if (s.entry) pushEntry();
      else if (!s.tokens.length || last().op || last().open) s.tokens.push({ num: s.value });
      if (last().open) s.tokens.pop();
      for (let n = openCount(); n > 0; n--) s.tokens.push({ close: true });
      const text = expression() + ' =';
      const r = evaluate(s.tokens);
      if (r === null) {
        s.error = DIVIDE_BY_ZERO;
        s.tokens = [];
        s.entry = '';
        return;
      }
      s.value = r;
      s.lastExpression = text;
      s.tokens = [];
      s.justEvaluated = true;
    }

    function expression() {
      if (s.justEvaluated) return s.lastExpression;
      let out = '';
      for (const t of s.tokens) {
        if (t.num !== undefined) {
          const f = format(t.num, s.base);
          out += (f.startsWith('-') ? '(' + f + ')' : f) + ' ';
        } else if (t.op) out += t.op + ' ';
        else if (t.open) out += '(';
        else {
          if (out.endsWith(' ')) out = out.slice(0, -1);
          out += ') ';
        }
      }
      return out.trim();
    }

    function press(key) {
      if (BASES[key]) { setBase(BASES[key]); return; }
      if (key === 'AC') { clear(); return; }
      if (s.error !== null) {
        if (key === 'CE' || key === '⌫') { clear(); return; }
        if (digitValue(key) >= 0) clear();
        else return;
      }
      if (digitValue(key) >= 0) { digit(key); return; }
      switch (key) {
        case '⌫': backspace(); return;
        case 'CE': s.entry = ''; s.value = 0n; return;
        case '±': unary(v => wrap(-v)); return;
        case 'NOT': unary(v => ~v); return;
        case '(': openParen(); return;
        case ')': closeParen(); return;
        case '=': equals(); return;
        default: if (PRECEDENCE[key]) operator(key);
      }
    }

    return {
      state: s,
      press,
      clear,
      setBase,
      expression,
      display: () => s.error !== null ? s.error : group(s.entry || format(s.value, s.base), s.base),
      /** Значение во всех четырёх системах, с разбивкой. */
      all: () => ({
        HEX: group(format(s.value, 16), 16),
        DEC: group(format(s.value, 10), 10),
        OCT: group(format(s.value, 8), 8),
        BIN: group(format(s.value, 2), 2),
      }),
    };
  }

  return { DIVIDE_BY_ZERO, BASES, PRECEDENCE, PROG_LAYOUT, digitValue, format, parse, group, apply, createProgrammer };
}));
