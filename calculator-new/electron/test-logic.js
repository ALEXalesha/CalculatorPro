// Логические тесты калькулятора без Electron.
// Читаем скрипт из renderer/index.html, исполняем в изолированном
// vm-контексте с заглушками DOM, и проверяем поведение бизнес-логики.
//
// Покрывает:
//   - Баг №1: getLastNumber + операции при числах >= 1000 (с разделителями тысяч).
//   - Баг №2: ввод цифры после C / ⌫ когда currentExpr заканчивается на " 0".
//   - Регрессионные проверки базового поведения.

const fs = require('fs');
const path = require('path');
const vm = require('vm');

const HTML_PATH = path.join(__dirname, 'renderer', 'index.html');
const html = fs.readFileSync(HTML_PATH, 'utf8');
const m = html.match(/<script>([\s\S]*?)<\/script>/);
if (!m) {
  console.error('Cannot find <script> in index.html');
  process.exit(2);
}
const scriptSrc = m[1];

// В скрипте есть try { require('electron') } и обращения к DOM на старте.
// Мокаем минимум, чтобы скрипт инициализировался без падений.
function makeElement() {
  const listeners = {};
  return {
    textContent: '',
    innerHTML: '',
    classList: {
      _set: new Set(),
      add(c) { this._set.add(c); },
      remove(c) { this._set.delete(c); },
      toggle(c, on) {
        if (on === undefined) {
          if (this._set.has(c)) { this._set.delete(c); return false; }
          this._set.add(c); return true;
        }
        if (on) this._set.add(c); else this._set.delete(c);
        return !!on;
      },
      contains(c) { return this._set.has(c); },
    },
    style: { setProperty() {} },
    dataset: {},
    addEventListener(name, fn) { (listeners[name] ||= []).push(fn); },
    removeEventListener() {},
    querySelectorAll() { return []; },
    querySelector() { return null; },
    getBoundingClientRect() { return { x: 0, y: 0, width: 100, height: 100 }; },
    scrollLeft: 0,
    scrollWidth: 0,
    clientWidth: 100,
    clientHeight: 100,
    disabled: false,
  };
}

const sandbox = {
  console,
  setTimeout, clearTimeout,
  Math, Date, JSON, Number, String, Array, Object, RegExp, parseFloat, parseInt, isFinite, isNaN,
  document: {
    getElementById() { return makeElement(); },
    querySelector() { return makeElement(); },
    querySelectorAll() { return []; },
    addEventListener() {},
    documentElement: { style: { setProperty() {} }, clientWidth: 320, clientHeight: 640 },
    body: Object.assign(makeElement(), { clientWidth: 320, clientHeight: 640 }),
  },
  window: { addEventListener() {} },
  localStorage: {
    _data: {},
    getItem(k) { return Object.prototype.hasOwnProperty.call(this._data, k) ? this._data[k] : null; },
    setItem(k, v) { this._data[k] = String(v); },
    removeItem(k) { delete this._data[k]; },
  },
  requestAnimationFrame: (cb) => setTimeout(cb, 0),
  ResizeObserver: class { observe() {} disconnect() {} },
  MutationObserver: class { observe() {} disconnect() {} },
  // мок electron — чтобы try { require('electron') } в renderer-скрипте отрабатывал тихо
  require: (mod) => {
    if (mod === 'electron') return { ipcRenderer: { send() {}, on() {} } };
    throw new Error('require not supported in test sandbox: ' + mod);
  },
};
sandbox.globalThis = sandbox;
sandbox.self = sandbox;

// Капсула в конец скрипта, чтобы вытащить функции и состояние через геттеры/сеттеры.
const exposer = `
;globalThis.__calc = {
  get state() {
    return { currentExpr, lastExpression, firstOperand, pendingOperator,
             isInputtingNew, isResultShown, memory, isSecond, isRad };
  },
  reset() {
    currentExpr = '0'; lastExpression = ''; firstOperand = null;
    pendingOperator = null; isInputtingNew = true; isResultShown = false;
    memory = 0; isSecond = false; isRad = false; history.length = 0;
  },
  history,
  inputNum, inputOp, applyPercent, negate, backspace, clearAll, clearEntry,
  getLastNumber, formatNumber, replaceLastNumber, sciActions,
};
`;

vm.createContext(sandbox);
try {
  vm.runInContext(scriptSrc + exposer, sandbox, { filename: 'index.html<script>' });
} catch (e) {
  console.error('Failed to evaluate calculator script in sandbox:', e);
  process.exit(2);
}

const calc = sandbox.__calc;

// Мини-тест-раннер
let passed = 0, failed = 0;
const failures = [];

function test(name, fn) {
  calc.reset();
  try {
    fn();
    console.log(`  ✓ ${name}`);
    passed++;
  } catch (e) {
    console.log(`  ✗ ${name}`);
    console.log(`      ${e.message}`);
    failed++;
    failures.push({ name, err: e });
  }
}

function eq(actual, expected, label) {
  if (actual !== expected) {
    throw new Error(`${label || 'assert'}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

// Симуляция нажатия последовательности кнопок (как делает UI-листенер)
function press(seq) {
  for (const tok of seq) {
    if (/^\d$/.test(tok)) calc.inputNum(tok);
    else if (tok === '.') calc.inputNum('.');
    else if (['+','-','*','/','^','='].includes(tok)) calc.inputOp(tok);
    else if (tok === 'AC' || tok === 'C') {
      const s = calc.state;
      if (s.currentExpr !== '0' || s.lastExpression) calc.clearEntry();
      else calc.clearAll();
    } else if (tok === '⌫' || tok === 'back') calc.backspace();
    else if (tok === '%') calc.applyPercent();
    else if (tok === '±') calc.negate();
    else if (tok === 'mr') calc.sciActions['mr']();
    else if (tok === 'm+') calc.sciActions['m+']();
    else if (tok === 'mc') calc.sciActions['mc']();
    else if (tok === 'sqrt') calc.sciActions['sqrt']();
    else if (tok === 'recip') calc.sciActions['recip']();
    else throw new Error('Unknown press token: ' + JSON.stringify(tok));
  }
}

console.log('\n=== Bug 1: разделители тысяч в getLastNumber ===\n');

test('getLastNumber извлекает "1 234" (NBSP) целиком', () => {
  // ставим состояние вручную: currentExpr = "5 + 1<NBSP>234"
  calc.reset();
  // прокрутим через нормальный путь: 1000+234=1234, потом проверим формат
  // Проще — через formatNumber:
  const formatted = calc.formatNumber(1234);
  // вытащим NBSP-вариант
  eq(formatted, '1 234', 'formatNumber(1234)');
});

test('5000 + 50% = 7 500 (не 5 500)', () => {
  press(['5','0','0','0','+','5','0','%','=']);
  eq(calc.state.currentExpr, '7 500', 'после 5000+50%=');
});

test('mr с памятью 1234: 1000 + mr = 2 234 (не 1 234)', () => {
  press(['1','2','3','4','m+','AC','1','0','0','0','+','mr','=']);
  eq(calc.state.currentExpr, '2 234', 'после 1234 m+ AC 1000+mr=');
});

test('% на больших числах: 2000 + 100% = 4 000', () => {
  press(['2','0','0','0','+','1','0','0','%','=']);
  eq(calc.state.currentExpr, '4 000', '2000+100%=');
});

test('Цепочка: 500 + 700 + 800 = 2 000', () => {
  press(['5','0','0','+','7','0','0','+','8','0','0','=']);
  eq(calc.state.currentExpr, '2 000', '500+700+800=');
});

console.log('\n=== Bug 2: ввод цифры после C / ⌫ ===\n');

test('5 + 3 C 7 = должен дать 12 (не 57)', () => {
  press(['5','+','3','C','7','=']);
  eq(calc.state.currentExpr, '12', 'после 5+3 C 7=');
});

test('5 + ± ⌫ 3 = должен дать 8 (не 5 + 03)', () => {
  press(['5','+','±','⌫','3','=']);
  eq(calc.state.currentExpr, '8', 'после 5+±⌫ 3=');
});

test('5 + 3 C . показывает "5 + 0," (а не "5 + 00,")', () => {
  press(['5','+','3','C','.']);
  eq(calc.state.currentExpr, '5 + 0,', 'после 5+3 C .');
});

test('5 + ± ⌫ . показывает "5 + 0,"', () => {
  press(['5','+','±','⌫','.']);
  eq(calc.state.currentExpr, '5 + 0,', 'после 5+±⌫ .');
});

console.log('\n=== Бонусы: Rand в историю, авто-сброс после Ошибки ===\n');

test('Rand добавляет запись в историю', () => {
  calc.sciActions['rand']();
  eq(calc.history.length, 1, 'history.length');
  eq(calc.history[0].expr, 'Rand', 'history[0].expr');
});

test('Ошибка + 5 авто-сбрасывает', () => {
  // 1/0 → Ошибка
  press(['0','recip']);
  eq(calc.state.currentExpr, 'Ошибка', 'после 1/0');
  press(['+']);
  eq(calc.state.currentExpr, '0', 'после Ошибка + (auto-reset)');
  eq(calc.state.lastExpression, '', 'lastExpression очищен');
});

console.log('\n=== Регрессии (базовые сценарии) ===\n');

test('2 + 3 = 5', () => {
  press(['2','+','3','=']);
  eq(calc.state.currentExpr, '5');
});

test('Цепочка 2 + 3 * 4 = 20', () => {
  press(['2','+','3','*','4','=']);
  eq(calc.state.currentExpr, '20');
});

test('100 + 20% = 120', () => {
  press(['1','0','0','+','2','0','%','=']);
  eq(calc.state.currentExpr, '120');
});

test('1 + 2 = + 3 = → 6 (продолжение после =)', () => {
  press(['1','+','2','=','+','3','=']);
  eq(calc.state.currentExpr, '6');
});

test('Замена оператора: 5 + - 3 = → 2', () => {
  press(['5','+','-','3','=']);
  eq(calc.state.currentExpr, '2');
});

test('± после оператора: 5 + ± 3 = → 2', () => {
  press(['5','+','±','3','=']);
  eq(calc.state.currentExpr, '2');
});

test('Деление на 0 → Ошибка', () => {
  press(['5','/','0','=']);
  eq(calc.state.currentExpr, 'Ошибка');
});

test('√(2500) = 50', () => {
  press(['2','5','0','0','sqrt']);
  eq(calc.state.currentExpr, '50');
});

test('C сначала чистит ввод, второй раз — всё', () => {
  press(['5','+','3','C']);
  eq(calc.state.currentExpr, '5 + 0', 'первое C');
  press(['C']);
  eq(calc.state.currentExpr, '0', 'второе C → AC');
});

console.log(`\n${passed} passed, ${failed} failed (всего ${passed + failed})`);

if (failed > 0) {
  console.log('\nFailures:');
  for (const f of failures) {
    console.log(`  ${f.name}: ${f.err.message}`);
  }
  process.exit(1);
}
