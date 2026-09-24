// Размер окна (1.4.0): минимум 280x500 - меньше, чем у калькулятора Windows (320x500).
// Файл одинаковый в calcpro-glass и ios-calculator. Как раскладка укладывается в этот
// минимум, проверяет e2e в настоящем окне (npm run test:e2e); здесь - что main.js
// действительно разрешает такое окно и что размер по умолчанию не меньше минимума.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const MAIN = fs.readFileSync(path.join(__dirname, '..', 'main.js'), 'utf8');
const option = (name) => {
  const m = MAIN.match(new RegExp(`\\b${name}:\\s*(\\d+)`));
  assert.ok(m, `${name} в main.js`);
  return Number(m[1]);
};

test('the window can be made as small as 280x500', () => {
  assert.equal(option('minWidth'), 280);
  assert.equal(option('minHeight'), 500);
});

test('the minimum is no larger than that of the Windows calculator', () => {
  assert.ok(option('minWidth') <= 320 && option('minHeight') <= 500);
});

test('the default size is not below the minimum and stays 320x640', () => {
  assert.equal(option('width'), 320);
  assert.equal(option('height'), 640);
  assert.ok(option('width') >= option('minWidth') && option('height') >= option('minHeight'));
});

test('the window stays resizable', () => {
  assert.match(MAIN, /resizable:\s*true/);
});

// Размер и место окна между запусками (1.5.0): модуль одинаковый в обоих приложениях,
// main.js им пользуется и кладёт его в сборку.
test('window-state.js is the same file in both calculators', () => {
  const other = path.join(__dirname, '..', '..', path.basename(path.join(__dirname, '..')) === 'calcpro-glass' ? 'ios-calculator' : 'calcpro-glass');
  assert.equal(fs.readFileSync(path.join(__dirname, '..', 'window-state.js'), 'utf8'),
    fs.readFileSync(path.join(other, 'window-state.js'), 'utf8'));
});

test('main.js restores the window from the saved state and saves it on close', () => {
  assert.match(MAIN, /require\('\.\/window-state'\)/);
  assert.match(MAIN, /WindowState\.restore\(WindowState\.load\(/);
  assert.match(MAIN, /on\('close',[^\n]*WindowState\.save\(/);
  for (const event of ['resized', 'moved']) assert.ok(MAIN.includes(`'${event}'`), event);
});

test('window-state.js goes into the packaged app', () => {
  const pkg = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'package.json'), 'utf8'));
  assert.ok(pkg.build.files.includes('window-state.js'));
});
