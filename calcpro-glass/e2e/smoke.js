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
    width: 360, height: 700, show: false, frame: false, transparent: true,
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
  const keys = async list => { for (const k of list) await click(`[data-key="${k}"]`); };
  const text = sel => js(`document.querySelector(${JSON.stringify(sel)}).textContent.trim()`);

  check('engine loaded under CSP', await js('typeof window.CalcCore'), 'object');
  check('preload bridge present', await js('typeof window.electronAPI'), 'object');
  check('standard keypad rendered', await js('document.querySelectorAll("#calcKeys [data-key]").length'), 20);

  await keys(['2', '+', '3', '×', '4', '=']);
  check('2+3×4=', await text('#mainResult'), '14');
  check('history line', await text('#historyLine'), '2+3×4 =');

  await click('#tabScientific');
  check('scientific keypad rendered', await js('document.querySelectorAll("#calcKeys [data-key]").length'), 48);
  await keys(['AC', '√', '1', '6', '=']);
  check('√16=', await text('#mainResult'), '4');
  await keys(['AC', '−', '2', 'xʸ', '2', '=']);
  check('−2^2=', await text('#mainResult'), '-4');

  await click('#tabFraction');
  await keys(['a/b', '1', 'a/b', '2', '+', 'a/b', '1', 'a/b', '3', '=']);
  check('1/2+1/3 as fraction', await js('document.querySelector("#mainResult .frac-num").textContent + "/" + document.querySelector("#mainResult .frac-den").textContent'), '5/6');

  await click('#tabStandard');
  await click('#btnHistory');
  check('history panel lists results', await js('document.querySelectorAll("#historyList .history-item").length'), 4);

  const out = process.env.E2E_OUT || path.join(os.tmpdir(), 'calcpro-glass-e2e.png');
  await click('#btnHistory');
  await keys(['AC', '1', '2', '3', '4', '5', '÷', '7', '=']);
  await new Promise(r => setTimeout(r, 400));
  fs.writeFileSync(out, (await win.webContents.capturePage()).toPNG());
  console.log('screenshot:', out);

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
  const glassKeys = '#calcKeys button, .mode-tab, .angle-pill, .mem-btn, .win-controls button';
  for (const [tab, name] of [['#tabStandard', 'standard'], ['#tabScientific', 'scientific'], ['#tabFraction', 'fraction']]) {
    await click(tab);
    await new Promise(r => setTimeout(r, 350));
    check(`${name} at 280x500: every key fits`, await fits(glassKeys), 'ok');
  }
  // Длинный пример в истории узкого окна переносится целиком, а не обрезается «…».
  await click('#tabStandard');
  await keys(['AC', ...'123456789', '×', ...'987654321', '+', ...'111111111', '×', ...'222222222', '=']);
  await click('#btnHistory');
  await new Promise(r => setTimeout(r, 350));
  const longItem = await js(`(() => {
    const e = [...document.querySelectorAll('#historyList .history-item-expr')].find(x => x.textContent.length > 30);
    return { text: e.textContent.replace(/​/g, ''), fits: e.scrollWidth <= e.clientWidth + 1 };
  })()`);
  check('long example in history is whole', longItem.text, '123456789×987654321+111111111×222222222');
  check('long example in history is not clipped', longItem.fits, true);
  await click('#btnHistory');
  win.setSize(360, 700);
  await new Promise(r => setTimeout(r, 300));

  // Темы Paint Pro: меню, смена цветов, память о выборе, Escape закрывает список.
  const css = v => js(`getComputedStyle(document.documentElement).getPropertyValue(${JSON.stringify(v)}).trim()`);
  check('glass theme by default', await js('document.documentElement.getAttribute("data-theme")'), null);
  const glassBase = await css('--app-base');
  await click('#btnTheme');
  check('theme menu open', await js('document.getElementById("themeMenu").classList.contains("open")'), true);
  check('five themes as in Paint Pro', await js('[...document.querySelectorAll("#themeMenu .theme-item")].map(b => b.dataset.theme).join(",")'),
    'glass,formal,light,night,warm');
  await click('#themeMenu .theme-item[data-theme="light"]');
  check('light theme applied', await js('document.documentElement.getAttribute("data-theme")'), 'light');
  check('light background differs', (await css('--app-base')) !== glassBase, true);
  check('menu closed after choice', await js('document.getElementById("themeMenu").classList.contains("open")'), false);
  check('choice remembered', await js('localStorage.getItem("calc-theme")'), 'light');
  await keys(['AC', '4', '2']);
  await click('#btnTheme');
  await js('document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }))');
  check('Escape closes the theme list', await js('document.getElementById("themeMenu").classList.contains("open")'), false);
  check('Escape in the list does not clear the display', await js('document.querySelector("#mainResult").textContent.trim()'), '42');
  await win.webContents.reload();
  await new Promise(r => win.webContents.once('did-finish-load', r));
  check('theme survives a restart', await js('document.documentElement.getAttribute("data-theme")'), 'light');
  check('checkmark on the saved theme', await js('document.querySelector("#themeMenu .theme-item[aria-checked=true]").dataset.theme'), 'light');
  await js('localStorage.setItem("calc-theme", "no-such-theme")');
  await win.webContents.reload();
  await new Promise(r => win.webContents.once('did-finish-load', r));
  check('unknown saved theme falls back to glass', await js('document.documentElement.getAttribute("data-theme")'), null);

  check('no console errors', errors.join(' | '), '');
  console.log(failures ? `${failures} check(s) failed` : 'all e2e checks passed');
  app.exit(failures ? 1 : 0);
}).catch(e => { console.error(e); app.exit(1); });
