// End-to-end smoke test: runs the real page in Electron (hidden window, same
// preload and CSP as the app), clicks buttons and checks what is displayed.
// Usage: npm run test:e2e   (writes a screenshot to $E2E_OUT or the temp dir)
const { app, BrowserWindow } = require('electron');
const path = require('path');
const fs = require('fs');
const os = require('os');

const ROOT = path.join(__dirname, '..');
const errors = [];
let failures = 0;

function check(name, actual, expected) {
  const ok = actual === expected;
  if (!ok) failures++;
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}: ${JSON.stringify(actual)}${ok ? '' : ' (expected ' + JSON.stringify(expected) + ')'}`);
}

app.whenReady().then(async () => {
  setTimeout(() => { console.error('timeout'); app.exit(2); }, 30000);
  const win = new BrowserWindow({
    width: 360, height: 760, show: false, frame: false, transparent: true,
    webPreferences: {
      preload: path.join(ROOT, 'preload.js'), contextIsolation: true, nodeIntegration: false, sandbox: true,
      // In-memory session: no localStorage (history) carried over from earlier runs.
      partition: `e2e-${process.pid}-${Date.now()}`,
    },
  });
  win.webContents.on('console-message', (e, level, message) => {
    const lvl = e.level !== undefined ? e.level : level;
    if (lvl === 'error' || lvl === 3) errors.push(e.message !== undefined ? e.message : message);
  });
  await win.loadFile(path.join(ROOT, 'src', 'index.html'));

  const js = code => win.webContents.executeJavaScript(code);
  const click = sel => js(`(() => { const el = document.querySelector(${JSON.stringify(sel)}); if (!el) throw new Error('no ' + ${JSON.stringify(sel)}); el.click(); })()`);
  const num = async s => { for (const d of s) await click(`.btn[data-num="${d}"]`); };
  const op = o => click(`.btn[data-op="${o}"]`);
  const act = a => click(`.btn[data-act="${a}"]`);
  const result = () => js('document.getElementById("result").textContent');

  check('engine loaded under CSP', await js('typeof window.CalcEngine'), 'object');
  check('preload bridge present', await js('typeof window.windowAPI'), 'object');
  check('no Node.js in the page', await js('typeof require'), 'undefined');

  await num('2'); await op('+'); await num('3'); await op('*'); await num('4'); await op('=');
  check('2+3×4= (chain)', await result(), '20');
  check('grey line', await js('document.getElementById("expression").textContent'), '5 × 4 =');

  await act('clear'); await act('clear');
  await num('1234'); await op('+'); await num('1000'); await op('=');
  check('thousands separator', await result(), '2 234');

  await act('clear'); await act('clear');
  await num('5'); await op('+');
  check('operator highlighted', await js('document.querySelector(".btn.op.active")?.dataset.op'), '+');
  check('C label', await js('document.getElementById("clearBtn").textContent'), 'C');

  await click('#sciToggle');
  check('scientific pad visible', await js('document.getElementById("calc").classList.contains("scientific")'), true);
  await act('clear'); await act('clear');
  await num('2500'); await act('sqrt');
  check('√2500', await result(), '50');
  await act('2nd');
  check('2nd relabels sin', await js('document.getElementById("btnSin").textContent'), 'sin-1');

  await click('#menuBtn');
  await click('.menu-item[data-action="history"]');
  check('history panel open', await js('document.getElementById("historyPanel").classList.contains("open")'), true);
  check('history entries', await js('document.querySelectorAll("#historyList .history-item").length'), 3);
  await click('#historyBack');

  await act('2nd');
  await act('clear'); await act('clear');
  await num('987654321'); await op('/'); await num('7'); await op('=');
  await new Promise(r => setTimeout(r, 400));
  const out = process.env.E2E_OUT || path.join(os.tmpdir(), 'calculator-ios26-e2e.png');
  fs.writeFileSync(out, (await win.webContents.capturePage()).toPNG());
  console.log('screenshot:', out);

  // Окно по умолчанию (main.js, 320x640): клавиши на всю ширину и в научном режиме.
  // Раньше круглые клавиши там сжимались по высоте до 39 px и оставляли по бокам
  // пустые полосы по 65 px; теперь они вытягиваются в капсулы.
  win.setSize(320, 640);
  await new Promise(r => setTimeout(r, 400));
  const span = sel => js(`(() => {
    const r = [...document.querySelectorAll(${JSON.stringify(sel)})].map(b => b.getBoundingClientRect());
    const w = r.map(x => x.width), h = r.map(x => x.height);
    return { left: Math.round(Math.min(...r.map(x => x.left))), right: Math.round(innerWidth - Math.max(...r.map(x => x.right))),
             wide: Math.min(...w) >= Math.max(...h) };
  })()`);
  const keypad = await span('.keypad .btn');
  const sciPad = await span('.scientific-pad .btn');
  check('scientific 320x640: keypad margins at most 24px', Math.max(keypad.left, keypad.right) <= 24, true);
  check('scientific 320x640: sci pad spans the keypad', Math.abs(sciPad.left - keypad.left) + Math.abs(sciPad.right - keypad.right) <= 2, true);
  check('scientific 320x640: keys are capsules, not ovals on end', keypad.wide && sciPad.wide, true);
  await click('#sciToggle');
  await new Promise(r => setTimeout(r, 400));
  check('basic 320x640: keys stay round', await js(`(() => { const r = document.querySelector('.keypad .btn').getBoundingClientRect(); return Math.round(r.width) === Math.round(r.height); })()`), true);
  check('basic 320x640: keypad margins at most 24px', Math.max(...Object.values(await span('.keypad .btn')).slice(0, 2)) <= 24, true);

  // Минимальное окно (main.js: 280x500, меньше калькулятора Windows 320x500): в каждом
  // режиме каждая видимая кнопка целиком в окне, не ниже 20 px и с подписью, которая
  // в неё влезает.
  const MIN = { w: 280, h: 500 };
  check('main.js minimum is 280x500', /minWidth:\s*280,\s*\n\s*minHeight:\s*500,/.test(fs.readFileSync(path.join(ROOT, 'main.js'), 'utf8')), true);
  win.setSize(MIN.w, MIN.h);
  await new Promise(r => setTimeout(r, 400));
  const fits = (sel) => js(`(() => {
    const bad = [];
    for (const b of document.querySelectorAll(${JSON.stringify(sel)})) {
      if (!b.offsetParent) continue;
      const r = b.getBoundingClientRect();
      const name = (b.textContent.trim() || b.id || b.className).slice(0, 12);
      if (r.left < 0 || r.top < 0 || r.right > innerWidth + 0.5 || r.bottom > innerHeight + 0.5) bad.push(name + ' outside');
      else if (r.height < 20) bad.push(name + ' ' + Math.round(r.height) + 'px');
      else if (b.scrollWidth > b.clientWidth + 1) bad.push(name + ' label clipped');
    }
    return bad.join(', ') || 'ok';
  })()`);
  const iosKeys = '.btn, .win-btn, #menuBtn, #sciToggle';
  check('basic at 280x500: every key fits', await fits(iosKeys), 'ok');
  await click('#sciToggle');
  await new Promise(r => setTimeout(r, 400));
  check('scientific at 280x500: every key fits', await fits(iosKeys), 'ok');
  await click('#sciToggle');
  // Длинные числа в истории узкого окна переносятся, а не уезжают за край.
  await act('clear'); await act('clear');
  await num('1234567890123456'); await op('*'); await num('9876543210987654'); await op('=');
  await click('#menuBtn');
  await click('.menu-item[data-action="history"]');
  await new Promise(r => setTimeout(r, 400));
  check('long numbers in history are not clipped', await js(`[...document.querySelectorAll('#historyList .h-expr, #historyList .h-result')].every(e => e.scrollWidth <= e.clientWidth + 1)`), true);
  check('long numbers in history are whole', await js('document.querySelector("#historyList .h-expr").textContent'), '1234567890123456 × 9876543210987654 =');
  await click('#historyBack');

  // Темы Paint Pro в меню «•••»: смена цветов и память о выборе.
  const css = v => js(`getComputedStyle(document.documentElement).getPropertyValue(${JSON.stringify(v)}).trim()`);
  check('glass theme by default', await js('document.documentElement.getAttribute("data-theme")'), null);
  const glassBase = await css('--app-base');
  check('five themes in the menu', await js('[...document.querySelectorAll("#themeItems .theme-item")].map(b => b.dataset.theme).join(",")'),
    'glass,formal,light,night,warm');
  await click('#menuBtn');
  await click('#themeItems .theme-item[data-theme="warm"]');
  check('warm theme applied', await js('document.documentElement.getAttribute("data-theme")'), 'warm');
  check('warm background differs', (await css('--app-base')) !== glassBase, true);
  check('choice remembered', await js('localStorage.getItem("calc-theme")'), 'warm');
  check('checkmark moved', await js('document.querySelector("#themeItems .theme-item.active").dataset.theme'), 'warm');
  await win.webContents.reload();
  await new Promise(r => win.webContents.once('did-finish-load', r));
  check('theme survives a restart', await js('document.documentElement.getAttribute("data-theme")'), 'warm');

  check('no console errors', errors.join(' | '), '');
  console.log(failures ? `${failures} check(s) failed` : 'all e2e checks passed');
  app.exit(failures ? 1 : 0);
}).catch(e => { console.error(e); app.exit(1); });
